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
using log4net;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;

namespace Simian.Protocols.Linden.Packets
{
    [SceneModule("Parcels")]
    public class Parcels : ISceneModule
    {
        private class OwnershipInfo
        {
            public int Count;
            public bool IsGroupOwned;
            public bool OnlineStatus;
        }

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private LLUDP m_udp;
        private IParcels m_parcels;
        private LLPermissions m_permissions;

        public void Start(IScene scene)
        {
            m_scene = scene;
            m_permissions = m_scene.GetSceneModule<LLPermissions>();

            m_parcels = m_scene.GetSceneModule<IParcels>();
            if (m_parcels == null)
            {
                m_log.Error("Cannot load Parcels without an IParcels");
                return;
            }

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.ParcelInfoRequest, ParcelInfoRequestHandler);
                m_udp.AddPacketHandler(PacketType.ParcelDwellRequest, ParcelDwellRequestHandler);
                m_udp.AddPacketHandler(PacketType.ParcelObjectOwnersRequest, ParcelObjectOwnersRequestHandler);
                m_udp.AddPacketHandler(PacketType.ParcelPropertiesRequest, ParcelPropertiesRequestHandler);
                m_udp.AddPacketHandler(PacketType.ParcelPropertiesRequestByID, ParcelPropertiesRequestByIDHandler);
                m_udp.AddPacketHandler(PacketType.ParcelPropertiesUpdate, ParcelPropertiesUpdateHandler);
                m_udp.AddPacketHandler(PacketType.ParcelAccessListRequest, ParcelAccessListRequestHandler);
                m_udp.AddPacketHandler(PacketType.ParcelAccessListUpdate, ParcelAccessListUpdateHandler);
                m_udp.AddPacketHandler(PacketType.ParcelSetOtherCleanTime, ParcelSetOtherCleanTimeHandler);
                m_udp.AddPacketHandler(PacketType.ParcelDivide, ParcelDivideHandler);
                m_udp.AddPacketHandler(PacketType.ParcelJoin, ParcelJoinHandler);
                m_udp.AddPacketHandler(PacketType.ParcelDeedToGroup, ParcelDeedToGroupHandler);
                m_udp.AddPacketHandler(PacketType.ParcelRelease, ParcelReleaseHandler);
                m_udp.AddPacketHandler(PacketType.ParcelBuy, ParcelBuyHandler);
                m_udp.AddPacketHandler(PacketType.ParcelBuyPass, ParcelBuyPassHandler);
                m_udp.AddPacketHandler(PacketType.ParcelSelectObjects, ParcelSelectObjectsHandler);
                m_udp.AddPacketHandler(PacketType.ParcelDisableObjects, ParcelDisableObjectsHandler);
                m_udp.AddPacketHandler(PacketType.ParcelReturnObjects, ParcelReturnObjectsHandler);
                m_udp.AddPacketHandler(PacketType.ParcelGodForceOwner, ParcelGodForceOwnerHandler);
                m_udp.AddPacketHandler(PacketType.ParcelGodMarkAsContent, ParcelGodMarkAsContentHandler);

                m_scene.OnPresenceAdd += PresenceAddHandler;
                m_scene.OnEntitySignificantMovement += EntitySignificantMovementHandler;
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.ParcelInfoRequest, ParcelInfoRequestHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelDwellRequest, ParcelDwellRequestHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelObjectOwnersRequest, ParcelObjectOwnersRequestHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelPropertiesRequest, ParcelPropertiesRequestHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelPropertiesRequestByID, ParcelPropertiesRequestByIDHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelPropertiesUpdate, ParcelPropertiesUpdateHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelAccessListRequest, ParcelAccessListRequestHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelAccessListUpdate, ParcelAccessListUpdateHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelSetOtherCleanTime, ParcelSetOtherCleanTimeHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelDivide, ParcelDivideHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelJoin, ParcelJoinHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelDeedToGroup, ParcelDeedToGroupHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelRelease, ParcelReleaseHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelBuy, ParcelBuyHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelBuyPass, ParcelBuyPassHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelSelectObjects, ParcelSelectObjectsHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelDisableObjects, ParcelDisableObjectsHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelReturnObjects, ParcelReturnObjectsHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelGodForceOwner, ParcelGodForceOwnerHandler);
                m_udp.RemovePacketHandler(PacketType.ParcelGodMarkAsContent, ParcelGodMarkAsContentHandler);

                m_scene.OnPresenceAdd -= PresenceAddHandler;
                m_scene.OnEntitySignificantMovement -= EntitySignificantMovementHandler;
            }
        }

        #region Packet Handlers

        private void ParcelInfoRequestHandler(Packet packet, LLAgent agent)
        {
            ParcelInfoRequestPacket request = (ParcelInfoRequestPacket)packet;

            SceneParcel parcel;
            if (m_parcels.TryGetParcel(request.Data.ParcelID, out parcel))
            {
                Vector3d scenePosition = m_scene.MinPosition;
                Vector3 aabbMin, aabbMax;

                ParcelInfoReplyPacket reply = new ParcelInfoReplyPacket();
                reply.AgentData.AgentID = agent.ID;
                reply.Data.ActualArea = ParcelManager.GetParcelArea(parcel, out aabbMin, out aabbMax);
                reply.Data.AuctionID = 0;
                reply.Data.BillableArea = reply.Data.ActualArea;
                reply.Data.Desc = Utils.StringToBytes(parcel.Desc);
                reply.Data.Dwell = parcel.Dwell;
                reply.Data.Flags = (byte)parcel.Flags;
                reply.Data.GlobalX = (float)scenePosition.X + aabbMin.X;
                reply.Data.GlobalY = (float)scenePosition.Y + aabbMin.Y;
                reply.Data.GlobalZ = 0f; // FIXME:
                reply.Data.Name = Utils.StringToBytes(parcel.Name);
                reply.Data.OwnerID = parcel.OwnerID;
                reply.Data.ParcelID = parcel.ID;
                reply.Data.SalePrice = parcel.SalePrice;
                reply.Data.SimName = Utils.StringToBytes(m_scene.Name);
                reply.Data.SnapshotID = parcel.SnapshotID;

                m_udp.SendPacket(agent, reply, ThrottleCategory.Task, false);
            }
            else
            {
                m_log.Warn(agent.Name + " requested info for unknown parcel " + request.Data.ParcelID);
            }
        }

        private void ParcelDwellRequestHandler(Packet packet, LLAgent agent)
        {
            ParcelDwellRequestPacket request = (ParcelDwellRequestPacket)packet;

            SceneParcel parcel;
            if (m_parcels.TryGetParcel(request.Data.ParcelID, out parcel) || m_parcels.TryGetParcel(request.Data.LocalID, out parcel))
            {
                ParcelDwellReplyPacket reply = new ParcelDwellReplyPacket();
                reply.AgentData.AgentID = agent.ID;
                reply.Data.Dwell = parcel.Dwell;
                reply.Data.LocalID = parcel.LocalID;
                reply.Data.ParcelID = parcel.ID;

                m_udp.SendPacket(agent, reply, ThrottleCategory.Task, false);
            }
            else
            {
                m_log.Warn(agent.Name + " requested dwell for unknown parcel " + request.Data.ParcelID + " (" + request.Data.LocalID + ")");
            }
        }

        private void ParcelObjectOwnersRequestHandler(Packet packet, LLAgent agent)
        {
            ParcelObjectOwnersRequestPacket request = (ParcelObjectOwnersRequestPacket)packet;

            SceneParcel parcel;
            if (m_parcels.TryGetParcel(request.ParcelData.LocalID, out parcel))
            {
                ParcelObjectOwnersReplyPacket reply = new ParcelObjectOwnersReplyPacket();
                Dictionary<UUID, OwnershipInfo> owners = new Dictionary<UUID, OwnershipInfo>(); 

                lock (parcel.ParcelEntities)
                {
                    foreach (ISceneEntity entity in parcel.ParcelEntities.Values)
                    {
                        // Skip child entities
                        if (entity is ILinkable && ((ILinkable)entity).Parent != null)
                            continue;

                        OwnershipInfo count;
                        if (!owners.TryGetValue(entity.OwnerID, out count))
                        {
                            count = new OwnershipInfo();
                            count.IsGroupOwned = false; // FIXME: Need to track group ownership
                            count.OnlineStatus = false; // FIXME: m_permissions.IsOnline(agent.ID, entity.OwnerID);

                            owners.Add(entity.OwnerID, count);
                        }

                        ++count.Count;
                        
                    }
                }

                reply.Data = new ParcelObjectOwnersReplyPacket.DataBlock[owners.Count];
                int i = 0;
                foreach (KeyValuePair<UUID, OwnershipInfo> kvp in owners)
                {
                    reply.Data[i++] = new ParcelObjectOwnersReplyPacket.DataBlock
                    {
                        Count = kvp.Value.Count,
                        OwnerID = kvp.Key,
                        IsGroupOwned = kvp.Value.IsGroupOwned,
                        OnlineStatus = kvp.Value.OnlineStatus
                    };
                }

                m_udp.SendPacket(agent, reply, ThrottleCategory.Task, true);
            }
            else
            {
                m_log.Warn(agent.Name + " requested object owners for unknown parcel " + request.ParcelData.LocalID);
            }
        }

        private void ParcelPropertiesRequestHandler(Packet packet, LLAgent agent)
        {
            ParcelPropertiesRequestPacket request = (ParcelPropertiesRequestPacket)packet;

            HashSet<int> parcels = new HashSet<int>();

            // Convert the boundaries to integers
            int north = (int)Math.Round(request.ParcelData.North) / 4;
            int east = (int)Math.Round(request.ParcelData.East) / 4;
            int south = (int)Math.Round(request.ParcelData.South) / 4;
            int west = (int)Math.Round(request.ParcelData.West) / 4;

            // Find all of the parcels within the given boundaries
            int xLen = east - west;
            int yLen = north - south;

            for (int x = 0; x < xLen; x++)
            {
                for (int y = 0; y < yLen; y++)
                {
                    if (west + x < 64 && south + y < 64)
                    {
                        int currentParcelID = m_parcels.GetParcelID(west + x, south + y);
                        if (!parcels.Contains(currentParcelID))
                            parcels.Add(currentParcelID);
                    }
                }
            }

            ParcelResult result = ParcelResult.NoData;
            if (parcels.Count == 1)
                result = ParcelResult.Single;
            else if (parcels.Count > 1)
                result = ParcelResult.Multiple;

            foreach (int parcelID in parcels)
                SendParcelProperties(parcelID, request.ParcelData.SequenceID, request.ParcelData.SnapSelection, result, agent);
        }

        private void ParcelPropertiesRequestByIDHandler(Packet packet, LLAgent agent)
        {
            ParcelPropertiesRequestByIDPacket request = (ParcelPropertiesRequestByIDPacket)packet;
            SendParcelProperties(request.ParcelData.LocalID, request.ParcelData.SequenceID, false, ParcelResult.Single, agent);
        }

        private void ParcelPropertiesUpdateHandler(Packet packet, LLAgent agent)
        {
            ParcelPropertiesUpdatePacket update = (ParcelPropertiesUpdatePacket)packet;

            SceneParcel parcel;
            if (m_parcels.TryGetParcel(update.ParcelData.LocalID, out parcel))
            {
                // TODO: Permissions check

                parcel.AuthBuyerID = update.ParcelData.AuthBuyerID;
                parcel.Category = (ParcelCategory)update.ParcelData.Category;
                parcel.Desc = Utils.BytesToString(update.ParcelData.Desc);
                parcel.Flags = (ParcelFlags)update.ParcelData.ParcelFlags;
                parcel.GroupID = update.ParcelData.GroupID;
                parcel.Landing = (LandingType)update.ParcelData.LandingType;
                parcel.Media.MediaAutoScale = update.ParcelData.MediaAutoScale != 0;
                parcel.Media.MediaID = update.ParcelData.MediaID;
                parcel.Media.MediaURL = Utils.BytesToString(update.ParcelData.MediaURL);
                parcel.MusicURL = Utils.BytesToString(update.ParcelData.MusicURL);
                parcel.Name = Utils.BytesToString(update.ParcelData.Name);
                parcel.PassHours = update.ParcelData.PassHours;
                parcel.PassPrice = update.ParcelData.PassPrice;
                parcel.SalePrice = update.ParcelData.SalePrice;
                parcel.SnapshotID = update.ParcelData.SnapshotID;
                parcel.LandingLocation = update.ParcelData.UserLocation;
                parcel.LandingLookAt = update.ParcelData.UserLookAt;

                m_parcels.AddOrUpdateParcel(parcel);

                if (update.ParcelData.Flags != 0)
                    SendParcelProperties(parcel.LocalID, 0, false, ParcelResult.Single, agent);
            }
            else
            {
                m_log.Warn("Got a ParcelPropertiesUpdate for an unknown parcel " + update.ParcelData.LocalID);
            }
        }

        private void ParcelAccessListRequestHandler(Packet packet, LLAgent agent)
        {
            ParcelAccessListRequestPacket request = (ParcelAccessListRequestPacket)packet;
            ParcelAccessFlags flags = (ParcelAccessFlags)request.Data.Flags;

            SceneParcel parcel;
            if (m_parcels.TryGetParcel(request.Data.LocalID, out parcel))
            {
                ParcelAccessListReplyPacket reply = new ParcelAccessListReplyPacket();
                reply.Data.AgentID = agent.ID;
                reply.Data.Flags = request.Data.Flags;
                reply.Data.LocalID = request.Data.LocalID;
                reply.Data.SequenceID = request.Data.SequenceID;

                List<ParcelAccessEntry> list = (flags == ParcelAccessFlags.Access)
                    ? parcel.AccessWhiteList
                    : parcel.AccessBlackList;

                if (list != null)
                {
                    lock (list)
                    {
                        reply.List = new ParcelAccessListReplyPacket.ListBlock[list.Count];
                        for (int i = 0; i < list.Count; i++)
                        {
                            reply.List[i] = new ParcelAccessListReplyPacket.ListBlock
                            {
                                Flags = request.Data.Flags,
                                ID = list[i].AgentID,
                                Time = (int)Utils.DateTimeToUnixTime(list[i].Created)
                            };
                        }
                    }
                }
                else
                {
                    reply.List = new ParcelAccessListReplyPacket.ListBlock[0];
                }

                m_udp.SendPacket(agent, reply, ThrottleCategory.Task, true);
            }
            else
            {
                m_log.Warn(agent.Name + " requested access list for unknown parcel " + request.Data.LocalID);
            }
        }

        private void ParcelAccessListUpdateHandler(Packet packet, LLAgent agent)
        {
            ParcelAccessListUpdatePacket update = (ParcelAccessListUpdatePacket)packet;
            ParcelAccessFlags flags = (ParcelAccessFlags)update.Data.Flags;

            SceneParcel parcel;
            if (m_parcels.TryGetParcel(update.Data.LocalID, out parcel))
            {
                // Initialize the white/black lists if they are not already
                if (parcel.AccessWhiteList == null)
                    parcel.AccessWhiteList = new List<ParcelAccessEntry>();
                if (parcel.AccessBlackList == null)
                    parcel.AccessBlackList = new List<ParcelAccessEntry>();

                List<ParcelAccessEntry> list = (flags == ParcelAccessFlags.Access)
                    ? parcel.AccessWhiteList
                    : parcel.AccessBlackList;

                lock (list)
                {
                    for (int i = 0; i < update.List.Length; i++)
                    {
                        UUID newEntry = update.List[i].ID;
                        bool found = false;

                        foreach (ParcelAccessEntry pae in list)
                        {
                            if (pae.AgentID == newEntry)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                            list.Add(new ParcelAccessEntry { AgentID = newEntry, Created = DateTime.UtcNow });
                    }
                }
            }
            else
            {
                m_log.Warn(agent.Name + " tried to update access list for unknown parcel" + update.Data.LocalID);
            }
        }

        private void ParcelSetOtherCleanTimeHandler(Packet packet, LLAgent agent)
        {
            ParcelSetOtherCleanTimePacket set = (ParcelSetOtherCleanTimePacket)packet;

            SceneParcel parcel;
            if (m_parcels.TryGetParcel(set.ParcelData.LocalID, out parcel))
            {
                parcel.AutoReturnTime = Utils.Clamp(set.ParcelData.OtherCleanTime, 0, Int32.MaxValue);
            }
            else
            {
                m_log.Warn(agent.Name + " tried to update auto return time for unknown parcel" + set.ParcelData.LocalID);
            }
        }

        private void ParcelDivideHandler(Packet packet, LLAgent agent)
        {
            ParcelDividePacket divide = (ParcelDividePacket)packet;

            int startX = (int)Math.Round(divide.ParcelData.West) / 4;
            int startY = (int)Math.Round(divide.ParcelData.South) / 4;
            int endX = ((int)Math.Round(divide.ParcelData.East) / 4) - 1;
            int endY = ((int)Math.Round(divide.ParcelData.North) / 4) - 1;

            if (startX < 0 || startY < 0 || endX < startX || endY < startY ||
                endX > 63 || endY > 63 || startX > endX || startY > endY)
            {
                m_log.Warn(agent.Name + String.Format(" sent invalid ParcelDivide: West {0} South {1} East {2} North {3}",
                    divide.ParcelData.West, divide.ParcelData.South, divide.ParcelData.East, divide.ParcelData.North));
                return;
            }

            // Make sure only a single parcel was selected
            int startParcelID = m_parcels.GetParcelID(startX, startY);
            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    int parcelID = m_parcels.GetParcelID(x, y);
                    if (parcelID != startParcelID)
                    {
                        m_scene.PresenceAlert(this, agent, "Only one parcel can be subdivided at a time");
                        return;
                    }
                }
            }

            SceneParcel parcel;
            if (!m_parcels.TryGetParcel(startParcelID, out parcel))
            {
                m_log.Warn("Failed to look up parcel " + startParcelID + " during parcel divide");
                return;
            }

            if (m_permissions != null && !m_permissions.CanEditParcel(agent, parcel))
            {
                m_scene.PresenceAlert(this, agent, "You do not have permission to subdivide this parcel");
                return;
            }

            m_parcels.SplitParcel(parcel, startX, endX, startY, endY);

            // Broadcast the new parcel overlay info
            m_scene.ForEachPresence(SendParcelOverlay);
        }

        private void ParcelJoinHandler(Packet packet, LLAgent agent)
        {
            ParcelJoinPacket join = (ParcelJoinPacket)packet;

            int startX = (int)Math.Round(join.ParcelData.West) / 4;
            int startY = (int)Math.Round(join.ParcelData.South) / 4;
            int endX = ((int)Math.Round(join.ParcelData.East) / 4) - 1;
            int endY = ((int)Math.Round(join.ParcelData.North) / 4) - 1;

            if (startX < 0 || startY < 0 || endX < startX || endY < startY ||
                endX > 63 || endY > 63 || startX > endX || startY > endY)
            {
                m_log.Warn(agent.Name + String.Format(" sent invalid ParcelJoin: West {0} South {1} East {2} North {3}",
                    join.ParcelData.West, join.ParcelData.South, join.ParcelData.East, join.ParcelData.North));
                return;
            }

            int largestArea = 0;
            Vector3 aabbMin, aabbMax;

            // Get the selected parcels
            List<SceneParcel> selectedParcels = new List<SceneParcel>();
            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    SceneParcel parcel;
                    if (m_parcels.TryGetParcel(m_parcels.GetParcelID(x, y), out parcel))
                    {
                        if (!selectedParcels.Contains(parcel) && m_permissions.CanEditParcel(agent, parcel))
                        {
                            // Largest parcel is the "master" parcel that smaller parcels are joined into
                            int area = ParcelManager.GetParcelArea(parcel, out aabbMin, out aabbMax);
                            if (area > largestArea)
                            {
                                largestArea = area;
                                selectedParcels.Insert(0, parcel);
                            }
                            else
                            {
                                selectedParcels.Add(parcel);
                            }
                        }
                    }
                }
            }

            // Enough parcels selected check
            if (selectedParcels.Count < 2)
            {
                m_scene.PresenceAlert(this, agent, "Not enough leased parcels in selection to join");
                return;
            }

            // Same owner check
            for (int i = 1; i < selectedParcels.Count; i++)
            {
                if (selectedParcels[i].OwnerID != selectedParcels[0].OwnerID)
                {
                    m_scene.PresenceAlert(this, agent, "All parcels must have the same owner before joining");
                    return;
                }
            }

            m_parcels.JoinParcels(selectedParcels);

            // Broadcast the new parcel overlay info
            m_scene.ForEachPresence(SendParcelOverlay);
        }

        private void ParcelDeedToGroupHandler(Packet packet, LLAgent agent)
        {
            ParcelDeedToGroupPacket deed = (ParcelDeedToGroupPacket)packet;

            SceneParcel parcel;
            if (m_parcels.TryGetParcel(deed.Data.LocalID, out parcel))
            {
                if (m_permissions == null || m_permissions.IsInGroup(agent, deed.Data.GroupID))
                {
                    parcel.GroupID = deed.Data.GroupID;
                    parcel.IsGroupOwned = true;
                }
                else
                {
                    m_log.Warn(agent.Name + " tried to deed parcel" + parcel.ID + " to group " + deed.Data.GroupID +
                        " that they are not a member of");
                }
            }
            else
            {
                m_log.Warn(agent.Name + " tried to deed unknown parcel " + deed.Data.LocalID);
            }
        }

        private void ParcelReleaseHandler(Packet packet, LLAgent agent)
        {
            // FIXME:
        }

        private void ParcelBuyHandler(Packet packet, LLAgent agent)
        {
            // FIXME:
        }

        private void ParcelBuyPassHandler(Packet packet, LLAgent agent)
        {
            // FIXME:
        }

        private void ParcelSelectObjectsHandler(Packet packet, LLAgent agent)
        {
            ParcelSelectObjectsPacket select = (ParcelSelectObjectsPacket)packet;

            SceneParcel parcel;
            if (m_parcels.TryGetParcel(select.ParcelData.LocalID, out parcel))
            {
                ObjectReturnType type = (ObjectReturnType)select.ParcelData.ReturnType;

                Predicate<ISceneEntity> selectFilter;

                switch (type)
                {
                    case ObjectReturnType.Owner:
                        selectFilter = delegate(ISceneEntity e)
                        {
                            if (e is ILinkable && ((ILinkable)e).Parent != null)
                                return false;
                            return e.OwnerID == parcel.OwnerID;
                        };
                        break;
                    case ObjectReturnType.Group:
                        // FIXME: Need to track group deeding to implement this
                        selectFilter = delegate(ISceneEntity e)
                        {
                            if (e is ILinkable && ((ILinkable)e).Parent != null)
                                return false;
                            return e.GroupID == parcel.GroupID;
                        };
                        break;
                    case ObjectReturnType.Other:
                        // FIXME: Need to track group deeding to implement this
                        selectFilter = delegate(ISceneEntity e)
                        {
                            if (e is ILinkable && ((ILinkable)e).Parent != null)
                                return false;
                            return e.OwnerID != parcel.OwnerID;
                        };
                        break;
                    case ObjectReturnType.Sell:
                        selectFilter = delegate(ISceneEntity e)
                        {
                            if (e is ILinkable && ((ILinkable)e).Parent != null)
                                return false;
                            return e is LLPrimitive && ((LLPrimitive)e).Prim.Properties.SaleType != SaleType.Not;
                        };
                        break;
                    case ObjectReturnType.List:
                        HashSet<UUID> selectIDs = new HashSet<UUID>();
                        for (int i = 0; i < select.ReturnIDs.Length; i++)
                            selectIDs.Add(select.ReturnIDs[i].ReturnID);
                        selectFilter = delegate(ISceneEntity e)
                        {
                            return selectIDs.Contains(e.ID);
                        };
                        break;
                    case ObjectReturnType.None:
                    default:
                        m_log.Warn(agent.Name + " sent an unrecognized select objects command " + type);
                        return;
                }

                // FIXME: What packet do we return?
            }
            else
            {
                m_log.Warn(agent.Name + " tried to select objects on unknown parcel " + select.ParcelData.LocalID);
            }
        }

        private void ParcelDisableObjectsHandler(Packet packet, LLAgent agent)
        {
            // FIXME: Implement this
        }

        private void ParcelReturnObjectsHandler(Packet packet, LLAgent agent)
        {
            // FIXME: Implement this
        }

        private void ParcelGodForceOwnerHandler(Packet packet, LLAgent agent)
        {
            // FIXME: Implement this
        }

        private void ParcelGodMarkAsContentHandler(Packet packet, LLAgent agent)
        {
            // FIXME: Implement this
        }

        #endregion Packet Handlers

        private void SendParcelProperties(int parcelID, int sequenceID, bool snapSelection, ParcelResult result,
            LLAgent agent)
        {
            SceneParcel parcel;
            if (m_parcels.TryGetParcel(parcelID, out parcel))
            {
                // Owner sanity check
                if (parcel.OwnerID == UUID.Zero)
                {
                    m_log.Warn("Assigning parcel " + parcel.Name + " to " + agent.Name);
                    parcel.OwnerID = agent.ID;
                    m_parcels.AddOrUpdateParcel(parcel);
                }

                // Claim date sanity check
                if (parcel.ClaimDate <= Utils.Epoch)
                {
                    m_log.Warn("Resetting invalid parcel claim date");
                    parcel.ClaimDate = DateTime.UtcNow;
                    m_parcels.AddOrUpdateParcel(parcel);
                }

                ParcelPropertiesMessage properties = new ParcelPropertiesMessage();
                properties.Area = ParcelManager.GetParcelArea(parcel, out properties.AABBMin, out properties.AABBMax);
                properties.AuctionID = 0; // Unused
                properties.AuthBuyerID = parcel.AuthBuyerID;
                properties.Bitmap = parcel.Bitmap;
                properties.Category = parcel.Category;
                properties.ClaimDate = parcel.ClaimDate;
                properties.ClaimPrice = 0; // Deprecated
                properties.Desc = parcel.Desc;
                properties.GroupID = parcel.GroupID;
                properties.IsGroupOwned = parcel.IsGroupOwned;
                properties.LandingType = parcel.Landing;
                properties.LocalID = parcel.LocalID;
                properties.MaxPrims = parcel.MaxPrims;
                properties.MediaAutoScale = parcel.Media.MediaAutoScale;
                properties.MediaDesc = parcel.Media.MediaDesc;
                properties.MediaHeight = parcel.Media.MediaHeight;
                properties.MediaID = parcel.Media.MediaID;
                properties.MediaLoop = parcel.Media.MediaLoop;
                properties.MediaType = parcel.Media.MediaType;
                properties.MediaURL = parcel.Media.MediaURL;
                properties.MediaWidth = parcel.Media.MediaWidth;
                properties.MusicURL = parcel.Media.MediaURL;
                properties.Name = parcel.Name;
                properties.ObscureMedia = parcel.ObscureMedia;
                properties.ObscureMusic = parcel.ObscureMusic;
                properties.OtherCleanTime = parcel.AutoReturnTime;
                properties.OwnerID = parcel.OwnerID;
                properties.ParcelFlags = parcel.Flags;
                properties.ParcelPrimBonus = 1f;
                properties.PassHours = parcel.PassHours;
                properties.PassPrice = parcel.PassPrice;
                properties.RegionDenyAgeUnverified = parcel.DenyAgeUnverified;
                properties.RegionDenyAnonymous = parcel.DenyAnonymous;
                properties.RegionDenyIdentified = false;
                properties.RegionDenyTransacted = false;
                properties.RegionPushOverride = parcel.PushOverride;
                properties.RentPrice = 0; // Deprecated
                properties.RequestResult = result;
                properties.SalePrice = parcel.SalePrice;
                properties.SequenceID = sequenceID;
                properties.SnapSelection = snapSelection;
                properties.SnapshotID = parcel.SnapshotID;
                properties.Status = parcel.Status;
                properties.UserLocation = parcel.LandingLocation;
                properties.UserLookAt = parcel.LandingLookAt;

                int ownerPrims = 0;
                int groupPrims = 0;
                int otherPrims = 0;
                int selectedPrims = 0;

                lock (parcel.ParcelEntities)
                {
                    foreach (ISceneEntity entity in parcel.ParcelEntities.Values)
                    {
                        // TODO: We don't currently track whether objects have been shared/deeded to group?
                        if (entity.OwnerID == parcel.OwnerID)
                            ++ownerPrims;
                        else
                            ++otherPrims;

                        // TODO: We don't currently track selected prims
                    }
                }

                properties.OwnerPrims = ownerPrims;
                properties.GroupPrims = groupPrims;
                properties.OtherPrims = otherPrims;
                properties.TotalPrims = ownerPrims + groupPrims + otherPrims;
                properties.SelectedPrims = selectedPrims;

                // TODO: Implement these
                properties.SimWideMaxPrims = 0;
                properties.SimWideTotalPrims = 0;

                // TODO: What are these?
                properties.SelfCount = 0;
                properties.PublicCount = 0;
                properties.OtherCount = 0;

                agent.EventQueue.QueueEvent("ParcelProperties", properties.Serialize());
            }
            else
            {
                m_log.Warn("SendParcelProperties() called for unknown parcel " + parcelID);
            }
        }

        private void PresenceAddHandler(object sender, PresenceArgs e)
        {
            SendParcelOverlay(e.Presence);
        }

        private void EntitySignificantMovementHandler(object sender, EntitySignificantMovementArgs e)
        {
            if (e.Entity is LLAgent)
            {
                LLAgent agent = (LLAgent)e.Entity;

                SceneParcel curParcel;
                if (m_parcels.TryGetParcel(agent.ScenePosition, out curParcel))
                {
                    SceneParcel lastParcel;
                    if (!m_parcels.TryGetParcel(agent.LastSignificantPosition, out lastParcel) || curParcel != lastParcel)
                        SendParcelProperties(curParcel.LocalID, 0, false, ParcelResult.Single, agent);
                }
            }
        }

        private void SendParcelOverlay(IScenePresence presence)
        {
            const int LAND_BLOCKS_PER_PACKET = 1024;

            if (m_udp == null)
                return;
            if (!(presence is LLAgent) || presence.InterestList == null)
                return;
            LLAgent agent = (LLAgent)presence;

            byte[] byteArray = new byte[LAND_BLOCKS_PER_PACKET];
            int byteArrayCount = 0;
            int sequenceID = 0;

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    byte tempByte = 0; // The flags for the current 4x4m parcel square

                    SceneParcel parcel;
                    if (m_parcels.TryGetParcel(m_parcels.GetParcelID(x, y), out parcel))
                    {
                        // Set the ownership/sale flag
                        if (parcel.OwnerID == presence.ID)
                            tempByte = (byte)ParcelOverlayType.OwnedBySelf;
                        //else if (parcel.AuctionID != 0)
                        //    tempByte = (byte)ParcelOverlayType.Auction;
                        else if (parcel.SalePrice > 0 && (parcel.AuthBuyerID == UUID.Zero || parcel.AuthBuyerID == presence.ID))
                            tempByte = (byte)ParcelOverlayType.ForSale;
                        else if (parcel.GroupID != UUID.Zero)
                            tempByte = (byte)ParcelOverlayType.OwnedByGroup;
                        else if (parcel.OwnerID != UUID.Zero)
                            tempByte = (byte)ParcelOverlayType.OwnedByOther;
                        else
                            tempByte = (byte)ParcelOverlayType.Public;

                        // Set the border flags
                        if (x == 0)
                            tempByte |= (byte)ParcelOverlayType.BorderWest;
                        else if (m_parcels.GetParcelID(x - 1, y) != parcel.LocalID)
                            // Parcel to the west is different from the current parcel
                            tempByte |= (byte)ParcelOverlayType.BorderWest;

                        if (y == 0)
                            tempByte |= (byte)ParcelOverlayType.BorderSouth;
                        else if (m_parcels.GetParcelID(x, y - 1) != parcel.LocalID)
                            // Parcel to the south is different from the current parcel
                            tempByte |= (byte)ParcelOverlayType.BorderSouth;

                        byteArray[byteArrayCount] = tempByte;
                        ++byteArrayCount;
                        if (byteArrayCount >= LAND_BLOCKS_PER_PACKET)
                        {
                            // Send a ParcelOverlay packet
                            ParcelOverlayPacket overlay = new ParcelOverlayPacket();
                            overlay.ParcelData.SequenceID = sequenceID;
                            overlay.ParcelData.Data = byteArray;
                            m_udp.SendPacket(agent, overlay, ThrottleCategory.Task, false);

                            byteArrayCount = 0;
                            ++sequenceID;
                        }
                    }
                    else
                    {
                        m_log.Warn("Parcel overlay references missing parcel " + m_parcels.GetParcelID(x, y));
                    }
                }
            }
        }
    }
}
