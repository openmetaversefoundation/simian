/*
 * Copyright (c) Open Metaverse Foundation
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using log4net;
using OpenMetaverse;

namespace Simian.Protocols.Linden
{
    public static class PermissionMaskExtensions
    {
        public static bool HasPermission(this PermissionMask mask, PermissionMask permission)
        {
            return (mask & permission) == permission;
        }
    }

    [SceneModule("LLPermissions")]
    public class LLPermissions : ISceneModule
    {
        private const int REQUEST_TIMEOUT = 1000 * 30;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IParcels m_parcels;
        private IUserClient m_userClient;
        private IInventoryClient m_inventoryClient;
        private IGroupsClient m_groupsClient;
        private IEstateClient m_estateClient;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_parcels = m_scene.GetSceneModule<IParcels>();

            m_userClient = m_scene.Simian.GetAppModule<IUserClient>();
            m_inventoryClient = m_scene.Simian.GetAppModule<IInventoryClient>();
            m_groupsClient = m_scene.Simian.GetAppModule<IGroupsClient>();
            m_estateClient = m_scene.Simian.GetAppModule<IEstateClient>();
        }

        public void Stop()
        {
        }

        public PrimFlags GetFlagsFor(IScenePresence presence, LLPrimitive entity)
        {
            PrimFlags flags = entity.Prim.Flags;
            Permissions perms = entity.Prim.Properties.Permissions;

            // Remove flags that shouldn't be sent to clients
            flags &= ~(PrimFlags.DieAtEdge | PrimFlags.Flying | PrimFlags.ReturnAtEdge | PrimFlags.Sandbox);

            if (entity.OwnerID != UUID.Zero)
                flags |= PrimFlags.ObjectAnyOwner; // Someone owns the object
            if (entity.GroupID != UUID.Zero)
                flags |= (PrimFlags.ObjectAnyOwner | PrimFlags.ObjectGroupOwned); // A group owns this object

            if (entity.OwnerID == presence.ID || IsGridAdmin(presence) || IsEstateManager(presence))
            {
                // User owner permissions

                // Mark that we own this object
                flags |= PrimFlags.ObjectYouOwner;
                flags |= PrimFlags.ObjectOwnerModify;

                if (perms.OwnerMask.HasPermission(PermissionMask.Copy))
                    flags |= PrimFlags.ObjectCopy;
                if (perms.OwnerMask.HasPermission(PermissionMask.Modify))
                    flags |= PrimFlags.ObjectModify;
                if (perms.OwnerMask.HasPermission(PermissionMask.Move))
                    flags |= PrimFlags.ObjectMove;
                if (perms.OwnerMask.HasPermission(PermissionMask.Transfer))
                    flags |= PrimFlags.ObjectTransfer;
            }
            else if (IsInGroup(presence, entity.GroupID))
            {
                // Use group permissions

                // Mark that we own this object
                flags |= PrimFlags.ObjectYouOwner;
                flags |= PrimFlags.ObjectOwnerModify;
                flags |= PrimFlags.ObjectYouOfficer;

                if (perms.GroupMask.HasPermission(PermissionMask.Copy))
                    flags |= PrimFlags.ObjectCopy;
                if (perms.GroupMask.HasPermission(PermissionMask.Modify))
                    flags |= PrimFlags.ObjectModify;
                if (perms.GroupMask.HasPermission(PermissionMask.Move))
                    flags |= PrimFlags.ObjectMove;
                if (perms.GroupMask.HasPermission(PermissionMask.Transfer))
                    flags |= PrimFlags.ObjectTransfer;
            }
            else
            {
                // Use everyone permissions

                if (perms.EveryoneMask.HasPermission(PermissionMask.Copy))
                    flags |= PrimFlags.ObjectCopy;
                if (perms.EveryoneMask.HasPermission(PermissionMask.Modify))
                    flags |= PrimFlags.ObjectModify;
                if (perms.EveryoneMask.HasPermission(PermissionMask.Move))
                    flags |= PrimFlags.ObjectMove;
                if (perms.EveryoneMask.HasPermission(PermissionMask.Transfer))
                    flags |= PrimFlags.ObjectTransfer;
            }

            return flags;
        }

        public Permissions GetDefaultPermissions()
        {
            return new Permissions(
                (uint)PermissionMask.All,
                (uint)PermissionMask.None,
                (uint)PermissionMask.None,
                (uint)PermissionMask.Transfer,
                (uint)PermissionMask.All);
        }

        public PermissionMask GetPrimPermissions(IScenePresence presence, LLPrimitive entity)
        {
            // Check if presence is a grid admin
            if (IsGridAdmin(presence))
                return PermissionMask.All;

            // Check if presence is an estate manager
            if (IsEstateManager(presence))
                return PermissionMask.All;

            Permissions perms = entity.Prim.Properties.Permissions;

            if (entity.OwnerID == presence.ID)
                return perms.OwnerMask;

            if (entity.GroupID != UUID.Zero && IsInGroup(presence, entity.GroupID))
                return perms.GroupMask;

            return perms.EveryoneMask;
        }

        public PermissionMask GetAssetPermissions(IScenePresence presence, UUID assetID)
        {
            // IF we are running without a connection to an inventory service, assume
            // all users have full access to assets
            if (m_inventoryClient == null)
                return PermissionMask.All;

            PermissionMask mask = PermissionMask.None;

            IList<InventoryItem> items;
            if (m_inventoryClient.TryGetItemsByAssetID(presence.ID, assetID, out items))
            {
                for (int i = 0; i < items.Count; i++)
                {
                    LLInventoryItem item = new LLInventoryItem(items[i]);
                    mask |= item.Permissions.OwnerMask;
                }
            }
            else
            {
                m_log.Warn("Failed to fetch inventory items for asset " + assetID);
            }

            return mask;
        }

        public bool CanCreateAt(IScenePresence presence, Vector3 location)
        {
            if (m_parcels == null)
                return true;

            SceneParcel parcel;
            if (m_parcels.TryGetParcel(location, out parcel))
            {
                // If we can't enter the parcel we can't create anything there either
                if (!CanEnterParcel(presence, parcel))
                    return false;

                // CreateObjects flag set? We're good
                if ((parcel.Flags & ParcelFlags.CreateObjects) == ParcelFlags.CreateObjects)
                    return true;

                // CreateGroupObjects flag set and we're a member of the parcel group? We're good
                if ((parcel.Flags & ParcelFlags.CreateGroupObjects) == ParcelFlags.CreateGroupObjects && IsInGroup(presence, parcel.GroupID))
                    return true;

                return false;
            }
            else
            {
                m_log.Warn("No parcel found at " + location);
                return true;
            }
        }

        public bool CanEditParcel(IScenePresence presence, SceneParcel parcel)
        {
            // Check for superuser
            if (IsGridAdmin(presence) || IsEstateManager(presence))
                return true;

            if (presence.ID == parcel.OwnerID)
                return true;

            if (parcel.IsGroupOwned && HasGroupPowers(presence, parcel.GroupID, GroupPowers.LandDivideJoin))
                return true;

            return false;
        }

        public bool CanEnterParcel(IScenePresence presence, SceneParcel parcel)
        {
            // Check for superuser
            if (IsGridAdmin(presence) || IsEstateManager(presence))
                return true;

            // Check for an unverified presence ban
            if ((parcel.Flags & ParcelFlags.DenyAnonymous) == ParcelFlags.DenyAnonymous && !presence.IsVerified)
                return false;

            // Check if UseAccessGroup is enabled and if so, is the presence a member of the correct group
            if ((parcel.Flags & ParcelFlags.UseAccessGroup) == ParcelFlags.UseAccessGroup && !IsInGroup(presence, parcel.GroupID))
                return false;

            // Is the whitelist enabled?
            if ((parcel.Flags & ParcelFlags.UseAccessList) == ParcelFlags.UseAccessList)
            {
                // Check the whitelist
                if (parcel.AccessWhiteList != null)
                {
                    lock (parcel.AccessWhiteList)
                    {
                        for (int i = 0; i < parcel.AccessWhiteList.Count; i++)
                        {
                            ParcelAccessEntry pae = parcel.AccessWhiteList[i];
                            if (pae.AgentID == presence.ID)
                                return true;
                        }
                    }
                }

                return false;
            }

            // Is the blacklist enabled?
            if ((parcel.Flags & ParcelFlags.UseBanList) == ParcelFlags.UseBanList)
            {
                // Check the blacklist
                if (parcel.AccessBlackList != null)
                {
                    lock (parcel.AccessBlackList)
                    {
                        for (int i = 0; i < parcel.AccessBlackList.Count; i++)
                        {
                            ParcelAccessEntry pae = parcel.AccessBlackList[i];
                            if (pae.AgentID == presence.ID)
                                return false;
                        }
                    }
                }
            }

            return true;
        }

        public void GetRegionPrimUsage(IScenePresence presence, Vector3 location, out int used, out int capacity)
        {
            used = 0;
            capacity = 0;

            if (m_parcels == null)
                return;

            SceneParcel currentParcel;
            if (m_parcels.TryGetParcel(location, out currentParcel))
            {
                int _capacity = 0;
                int _used = 0;

                // Count the total prim usage and prim capacity for the owner of this parcel in the current sim
                m_parcels.ForEachParcel(
                    delegate(SceneParcel parcel)
                    {
                        bool sameOwner = (currentParcel.IsGroupOwned)
                            ? (parcel.IsGroupOwned && parcel.GroupID == currentParcel.GroupID)
                            : (!parcel.IsGroupOwned && parcel.OwnerID == currentParcel.OwnerID);

                        if (sameOwner)
                        {
                            _capacity += parcel.MaxPrims;
                            Dictionary<UUID, ISceneEntity> entities = parcel.ParcelEntities;
                            if (entities != null)
                                _used += entities.Count;
                        }
                    }
                );

                capacity = _capacity;
                used = _used;
            }
            else
            {
                m_log.Warn("No parcel found at " + location);
            }
        }

        public bool IsGridAdmin(IScenePresence presence)
        {
            // If we are running without a connection to a user service, assume
            // all users have admin access
            if (m_userClient == null)
                return true;

            // Try to fetch the user account information and check if their
            // AccessLevel is 200 or higher (admin level)
            User user;
            if (m_userClient.TryGetUser(presence.ID, out user))
                return (user.AccessLevel >= 200);

            return false;
        }

        public bool IsEstateManager(IScenePresence presence)
        {
            // If we are running without a connection to an estate service,
            // assume no users are estate managers
            if (m_estateClient == null)
                return false;

            // Try to fetch this scene's estate and check if this presence is a
            // manager in it
            Estate estate;
            if (m_estateClient.TryGetEstate(m_scene.ID, out estate))
                return estate.ContainsManager(presence.ID);

            return false;
        }

        public bool IsInGroup(IScenePresence presence, UUID groupID)
        {
            // If we are running without a connection to a groups service,
            // noone can be in any group
            if (m_groupsClient == null)
                return false;

            // Try to fetch the group and see if this presence is a member of
            // it
            Group group;
            if (m_groupsClient.TryGetGroup(groupID, out group))
                return group.Members.ContainsKey(presence.ID);

            return false;
        }

        public bool HasGroupPowers(IScenePresence presence, UUID groupID, GroupPowers powers)
        {
            // If we are running without a connection to a groups service,
            // noone can be in any group
            if (m_groupsClient == null)
                return false;

            // Try to fetch the group and see if this presence is a member of
            // it
            Group group;
            if (m_groupsClient.TryGetGroup(groupID, out group))
            {
                // Try to fetch membership information for this presence in the
                // group
                GroupMember member;
                if (group.Members.TryGetValue(presence.ID, out member))
                {
                    GroupPowers aggregatePowers = GroupPowers.None;

                    for (int i = 0; i < member.Roles.Count; i++)
                    {
                        GroupPowers rolePowers = (GroupPowers)member.Roles[i].Attributes["powers"].AsInteger();
                        aggregatePowers |= rolePowers;
                    }

                    return (aggregatePowers & powers) == powers;
                }
            }

            return false;
        }

        public bool TryUploadOneAsset(IScenePresence presence, out string error)
        {
            // TODO: When we have a currency module this should ping it deduct the asset upload cost

            if (presence.IsVerified)
            {
                error = String.Empty;
                return true;
            }
            else
            {
                error = "You are logged in with an unverified account. Asset uploads are disabled.";
                return false;
            }
        }
    }
}
