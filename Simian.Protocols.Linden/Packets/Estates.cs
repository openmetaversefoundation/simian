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
using System.Globalization;
using System.Text;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace Simian.Protocols.Linden.Packets
{
    [Flags]
    public enum EstateAccessFlags : uint
    {
        AllowedAgents   = 1 << 0,
        AllowedGroups   = 1 << 1,
        BannedAgents    = 1 << 2,
        Managers        = 1 << 3
    }

    [Flags]
    public enum EstateAccessDeltaFlags : uint
    {
        ApplyToAllEstates       = 1 << 0,
        ApplyToManagedEstates   = 1 << 1,
        AllowedAgentAdd         = 1 << 2,
        AllowedAgentRemove      = 1 << 3,
        AllowedGroupAdd         = 1 << 4,
        AllowedGroupRemove      = 1 << 5,
        BannedAgentAdd          = 1 << 6,
        BannedAgentRemove       = 1 << 7,
        ManagerAdd              = 1 << 8,
        ManagerRemove           = 1 << 9,
        NoReply                 = 1 << 10
    }

    public static class EstateAccessDeltaFlagsExtensions
    {
        public static bool HasFlag(this EstateAccessDeltaFlags accessFlags, EstateAccessDeltaFlags flag)
        {
            return (accessFlags & flag) == flag;
        }
    }

    [SceneModule("Estates")]
    public class Estates : ISceneModule
    {
        private const string PRODUCT_NAME = "Mainland / Full Region";

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private LLUDP m_udp;
        private RegionInfo m_regionInfo;
        private IEstateClient m_estateClient;
        private LLPermissions m_permissions;
        private Estate m_estate;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_regionInfo = scene.GetSceneModule<RegionInfo>();
            m_permissions = scene.GetSceneModule<LLPermissions>();

            m_estateClient = scene.Simian.GetAppModule<IEstateClient>();
            if (m_estateClient != null)
            {
                if (!m_estateClient.TryGetEstate(scene.ID, out m_estate))
                {
                    // FIXME: Get the config file entry for this sim's estate name and join it or 
                    // create it if it doesn't exist
                }
            }

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.RequestRegionInfo, RequestRegionInfoHandler);
                m_udp.AddPacketHandler(PacketType.EstateCovenantRequest, EstateCovenantRequestHandler);
                m_udp.AddPacketHandler(PacketType.EstateOwnerMessage, EstateOwnerMessageHandler);
                m_udp.AddPacketHandler(PacketType.GodlikeMessage, GodlikeMessageHandler);
                m_udp.AddPacketHandler(PacketType.GodUpdateRegionInfo, GodUpdateRegionInfoHandler);
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.RequestRegionInfo, RequestRegionInfoHandler);
                m_udp.RemovePacketHandler(PacketType.EstateCovenantRequest, EstateCovenantRequestHandler);
                m_udp.RemovePacketHandler(PacketType.EstateOwnerMessage, EstateOwnerMessageHandler);
                m_udp.RemovePacketHandler(PacketType.GodlikeMessage, GodlikeMessageHandler);
                m_udp.RemovePacketHandler(PacketType.GodUpdateRegionInfo, GodUpdateRegionInfoHandler);
            }
        }

        private void RequestRegionInfoHandler(Packet packet, LLAgent agent)
        {
            RegionInfoPacket info = new RegionInfoPacket();
            info.AgentData.AgentID = agent.ID;
            info.RegionInfo.SimName = Utils.StringToBytes(m_scene.Name);
            info.RegionInfo.ParentEstateID = 1; // Hardcoded to what the viewer considers "mainland"
            info.RegionInfo.PricePerMeter = 1; // Dummy value

            if (m_regionInfo != null)
            {
                // Region settings
                info.RegionInfo.MaxAgents = (byte)Math.Min(255, m_regionInfo.MaxAgents);
                info.RegionInfo.WaterHeight = m_regionInfo.WaterHeight;
                info.RegionInfo2.HardMaxAgents = m_regionInfo.MaxAgents;
                info.RegionInfo2.HardMaxObjects = m_regionInfo.ObjectCapacity;
                info.RegionInfo2.MaxAgents32 = m_regionInfo.MaxAgents;
                info.RegionInfo2.ProductName = Utils.StringToBytes(PRODUCT_NAME);
                info.RegionInfo2.ProductSKU = Utils.EmptyBytes;
                info.RegionInfo.UseEstateSun = m_regionInfo.UseEstateSun;
            }
            else
            {
                info.RegionInfo2.ProductName = Utils.EmptyBytes;
                info.RegionInfo2.ProductSKU = Utils.EmptyBytes;
            }

            if (m_estate != null)
            {
                info.RegionInfo.RegionFlags = (uint)m_estate.EstateFlags;
                info.RegionInfo.BillableFactor = 0f;
                info.RegionInfo.EstateID = m_estate.ID;
                info.RegionInfo.ObjectBonusFactor = m_estate.ObjectBonusFactor;
                info.RegionInfo.RedirectGridX = 0; // TODO: What is this?
                info.RegionInfo.RedirectGridY = 0; //
                info.RegionInfo.SimAccess = (byte)m_estate.AccessFlags;
                info.RegionInfo.SunHour = m_estate.SunHour;
                info.RegionInfo.TerrainLowerLimit = m_estate.TerrainLowerLimit;
                info.RegionInfo.TerrainRaiseLimit = m_estate.TerrainRaiseLimit;
            }
            else
            {
                if (m_regionInfo != null)
                {
                    info.RegionInfo.RegionFlags = (uint)m_regionInfo.RegionFlags;
                    info.RegionInfo.SimAccess = (byte)m_regionInfo.SimAccess;
                }

                info.RegionInfo.TerrainRaiseLimit = 255f;
            }

            m_udp.SendPacket(agent, info, ThrottleCategory.Task, false);
        }

        private void EstateCovenantRequestHandler(Packet packet, LLAgent agent)
        {
            EstateCovenantRequestPacket request = (EstateCovenantRequestPacket)packet;

            EstateCovenantReplyPacket reply = new EstateCovenantReplyPacket();

            if (m_estate != null)
            {
                reply.Data.CovenantID = m_estate.CovenantID;
                reply.Data.CovenantTimestamp = m_estate.CovenantTimestamp;
                reply.Data.EstateName = Utils.StringToBytes(m_estate.Name);
                reply.Data.EstateOwnerID = m_estate.OwnerID;
            }
            else
            {
                reply.Data.EstateName = Utils.EmptyBytes;
            }

            m_udp.SendPacket(agent, reply, ThrottleCategory.Task, false);
        }

        private void EstateOwnerMessageHandler(Packet packet, LLAgent agent)
        {
            EstateOwnerMessagePacket message = (EstateOwnerMessagePacket)packet;
            string method = Utils.BytesToString(message.MethodData.Method);
            Estate estate;

            if (m_estateClient == null)
            {
                m_log.Warn("Ignoring estate owner message \"" + method + "\", no IEstateClient");
                return;
            }
            if (!m_estateClient.TryGetEstate(m_scene.ID, out estate))
            {
                m_log.Warn("Ignoring estate owner message \"" + method + "\", scene " + m_scene.Name + " has no estate");
                return;
            }
            if (m_permissions != null)
            {
                if (!m_permissions.IsEstateManager(agent))
                {
                    m_log.Warn("Ignoring estate owner message \"" + method + "\" from non-manager " + agent.Name);
                    return;
                }
            }

            string[] parameters = new string[message.ParamList.Length];

            for (int i = 0; i < message.ParamList.Length; i++)
                parameters[i] = Utils.BytesToString(message.ParamList[0].Parameter);

            switch (method)
            {
                case "getinfo":
                    SendDetailedEstateData(agent, estate, message.MethodData.Invoice);
                    SendEstateAccessList(agent, estate, EstateAccessFlags.Managers | EstateAccessFlags.AllowedAgents |
                        EstateAccessFlags.AllowedGroups | EstateAccessFlags.BannedAgents, message.MethodData.Invoice);
                    break;
                case "setregioninfo":
                    SetRegionInfo(agent, estate, parameters);
                    break;
                case" texturedetail":
                    SetTextureDetail(agent, estate, parameters);
                    break;
                case "textureheights":
                    SetTextureHeights(agent, estate, parameters);
                    break;
                case "texturecommit":
                    BroadcastRegionHandshake();
                    break;
                case "setregionterrain":
                    SetRegionTerrain(agent, estate, parameters);
                    break;
                case "restart":
                    //RegionRestart(parameters);
                    break;
                case "estatechangecovenantid":
                    ChangeCovenantID(agent, estate, parameters, message.MethodData.Invoice);
                    break;
                case "estateaccessdelta":
                    ChangeEstateAccess(agent, estate, parameters, message.MethodData.Invoice);
                    break;
                case "simulatormessage":
                    //SimulatorMessage(agent, estate, parameters);
                    break;
                case "instantmessage":
                    //InstantMessage(agent, estate, parameters);
                    break;
                case "setregiondebug":
                    //SetRegionDebug(agent, estate, parameters);
                    break;
                case "teleporthomeuser":
                    //TeleportHomeUser(agent, estate, parameters);
                    break;
                case "teleporthomeallusers":
                    //TeleportHomeAllUsers(agent, estate, parameters);
                    break;
                case "colliders":
                    //GetColliders(agent, estate, parameters);
                    break;
                case "scripts":
                    //GetScripts(agent, estate, parameters);
                    break;
                case "terrain":
                    //GetTerrain(agent, estate, parameters);
                    break;
                case "estatechangeinfo":
                    //EstateChangeInfo(agent, estate, parameters);
                    break;
                case "refreshmapvisibility":
                    //RefreshMapVisibility();
                    break;
                default:
                    m_log.Warn("Unrecognized EstateOwnerMessage \"" + method + "\" from " + agent.Name);
                    break;
            }
        }

        private void GodlikeMessageHandler(Packet packet, LLAgent agent)
        {
            GodlikeMessagePacket message = (GodlikeMessagePacket)packet;

            UUID invoiceID = message.MethodData.Invoice;
            string method = Utils.BytesToString(message.MethodData.Method);
            string[] parameters = new string[message.ParamList.Length];
            for (int i = 0; i < message.ParamList.Length; i++)
                parameters[i] = Utils.BytesToString(message.ParamList[i].Parameter);

            if (m_permissions != null)
            {
                if (!m_permissions.IsGridAdmin(agent))
                {
                    m_log.Warn("Ignoring GodlikeMessage " + method + " from non-admin " + agent.Name);
                    return;
                }
            }

            // DEBUG
            StringBuilder output = new StringBuilder(method + " (" + invoiceID + ")");
            for (int i = 0; i < parameters.Length; i++)
                output.AppendLine(" " + parameters[i]);
            m_log.Warn("Received an unhandled GodlikeMessage from " + agent.Name + ": " + output.ToString());
        }

        private void GodUpdateRegionInfoHandler(Packet packet, LLAgent agent)
        {
            GodUpdateRegionInfoPacket update = (GodUpdateRegionInfoPacket)packet;

            if (m_permissions != null)
            {
                if (!m_permissions.IsGridAdmin(agent))
                {
                    m_log.Warn("Ignoring GodUpdateRegionInfo from non-admin " + agent.Name);
                    return;
                }
            }
        }

        #region Estate Owner Message Handlers

        private void SetRegionInfo(LLAgent agent, Estate estate, string[] parameters)
        {
            if (parameters.Length != 9)
            {
                m_log.Warn(agent.Name + " sent a setregioninfo call with " + parameters.Length + " parameters");
                return;
            }

            bool blockTerraform = ParamStringToBool(parameters[0]);
            bool noFly = ParamStringToBool(parameters[1]);
            bool allowDamage = ParamStringToBool(parameters[2]);
            bool blockLandResell = !ParamStringToBool(parameters[3]);
            float maxAgents;
            Single.TryParse(parameters[4], NumberStyles.Float, Utils.EnUsCulture.NumberFormat, out maxAgents);
            float objectBonusFactor;
            Single.TryParse(parameters[5], NumberStyles.Float, Utils.EnUsCulture.NumberFormat, out objectBonusFactor);
            ushort matureLevel;
            UInt16.TryParse(parameters[6], NumberStyles.Integer, Utils.EnUsCulture.NumberFormat, out matureLevel);
            bool restrictPushObject = ParamStringToBool(parameters[7]);
            bool allowParcelChanges = ParamStringToBool(parameters[8]);

            estate.EstateFlags = ToggleRegionFlag(estate.EstateFlags, RegionFlags.BlockTerraform, blockTerraform);
            estate.EstateFlags = ToggleRegionFlag(estate.EstateFlags, RegionFlags.NoFly, noFly);
            estate.EstateFlags = ToggleRegionFlag(estate.EstateFlags, RegionFlags.AllowDamage, allowDamage);
            estate.EstateFlags = ToggleRegionFlag(estate.EstateFlags, RegionFlags.RestrictPushObject, restrictPushObject);
            estate.EstateFlags = ToggleRegionFlag(estate.EstateFlags, RegionFlags.AllowParcelChanges, allowParcelChanges);
            estate.MaxAgents = (uint)maxAgents;
            estate.MatureLevel = matureLevel;
            estate.ObjectBonusFactor = objectBonusFactor;

            m_estateClient.AddOrUpdateEstate(estate);

            BroadcastRegionInfo();
        }

        private void SetTextureDetail(LLAgent agent, Estate estate, string[] parameters)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                byte corner;
                UUID textureID;

                string[] args = parameters[i].Split(' ');
                if (args.Length == 2 && Byte.TryParse(args[0], out corner) && UUID.TryParse(args[1], out textureID))
                {
                    switch (corner)
                    {
                        case 0:
                            estate.TerrainDetail0 = textureID; break;
                        case 1:
                            estate.TerrainDetail1 = textureID; break;
                        case 2:
                            estate.TerrainDetail2 = textureID; break;
                        case 3:
                            estate.TerrainDetail3 = textureID; break;
                    }
                }
            }

            BroadcastRegionInfo();
        }

        private void SetTextureHeights(LLAgent agent, Estate estate, string[] parameters)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                byte corner;
                float start;
                float range;

                string[] args = parameters[i].Split(' ');
                if (args.Length == 3 && Byte.TryParse(args[0], out corner) && Single.TryParse(args[1], out start) && Single.TryParse(args[2], out range))
                {
                    switch (corner)
                    {
                        case 0:
                            estate.TerrainStartHeight00 = start;
                            estate.TerrainHeightRange00 = range;
                            break;
                        case 1:
                            estate.TerrainStartHeight01 = start;
                            estate.TerrainHeightRange01 = range;
                            break;
                        case 2:
                            estate.TerrainStartHeight10 = start;
                            estate.TerrainHeightRange10 = range;
                            break;
                        case 3:
                            estate.TerrainStartHeight11 = start;
                            estate.TerrainHeightRange11 = range;
                            break;
                    }
                }
            }

            BroadcastRegionInfo();
        }

        private void SetRegionTerrain(LLAgent agent, Estate estate, string[] parameters)
        {
            if (parameters.Length != 9)
            {
                m_log.Warn(agent.Name + " sent a setregionterrain call with " + parameters.Length + " parameters");
                return;
            }

            float waterHeight;
            Single.TryParse(parameters[0], NumberStyles.Float, Utils.EnUsCulture.NumberFormat, out waterHeight);
            float terrainRaiseLimit;
            Single.TryParse(parameters[1], NumberStyles.Float, Utils.EnUsCulture.NumberFormat, out terrainRaiseLimit);
            float terrainLowerLimit;
            Single.TryParse(parameters[2], NumberStyles.Float, Utils.EnUsCulture.NumberFormat, out terrainLowerLimit);
            bool useEstateSun = ParamStringToBool(parameters[3]);
            bool useFixedSun = ParamStringToBool(parameters[4]);
            float sunHour;
            Single.TryParse(parameters[5], NumberStyles.Float, Utils.EnUsCulture.NumberFormat, out sunHour);
            bool estateGlobalSun = ParamStringToBool(parameters[6]);
            bool estateFixedSun = ParamStringToBool(parameters[7]);
            float estateSunHour;
            Single.TryParse(parameters[8], NumberStyles.Float, Utils.EnUsCulture.NumberFormat, out estateSunHour);

            if (m_regionInfo != null)
            {
                m_regionInfo.WaterHeight = waterHeight;
                m_regionInfo.UseEstateSun = useEstateSun;
                m_regionInfo.UseFixedSun = useFixedSun;
            }

            estate.TerrainRaiseLimit = terrainRaiseLimit;
            estate.TerrainLowerLimit = terrainLowerLimit;
            estate.SunHour = sunHour;
            estate.UseGlobalSun = estateGlobalSun;
            estate.UseFixedSun = estateFixedSun;
        }

        private void RegionRestart()
        {
            // TODO: Implement this. Need to determine if this is a restart or restart cancel, and 
            // start the timer to send shutdown notices
        }

        private void ChangeCovenantID(LLAgent agent, Estate estate, string[] parameters, UUID invoiceID)
        {
            UUID covenantID;
            if (parameters.Length == 1 && UUID.TryParse(parameters[0], out covenantID))
            {
                estate.CovenantID = covenantID;
                estate.CovenantTimestamp = Utils.DateTimeToUnixTime(DateTime.UtcNow);

                m_estateClient.AddOrUpdateEstate(estate);
            }
            else
            {
                m_log.Warn(agent.Name + " sent an estatechangecovenantid call with unrecognized parameters");
            }
        }

        private void ChangeEstateAccess(LLAgent agent, Estate estate, string[] parameters, UUID invoiceID)
        {
            uint flagsValue;
            UUID agentOrGroupID;

            if (parameters.Length == 3 && UInt32.TryParse(parameters[1], out flagsValue) && UUID.TryParse(parameters[2], out agentOrGroupID))
            {
                EstateAccessDeltaFlags flags = (EstateAccessDeltaFlags)flagsValue;

                // Non-owners can only modify the ban list
                if (flags.HasFlag(EstateAccessDeltaFlags.AllowedAgentAdd) ||
                    flags.HasFlag(EstateAccessDeltaFlags.AllowedAgentRemove) ||
                    flags.HasFlag(EstateAccessDeltaFlags.AllowedGroupAdd) ||
                    flags.HasFlag(EstateAccessDeltaFlags.AllowedGroupRemove) ||
                    flags.HasFlag(EstateAccessDeltaFlags.ManagerAdd) ||
                    flags.HasFlag(EstateAccessDeltaFlags.ManagerRemove))
                {
                    if (agent.ID != estate.OwnerID && !m_permissions.IsGridAdmin(agent))
                    {
                        m_log.Warn("Non-manager " + agent.Name + " attempted to make " + flags + " changes to the estate access list");
                        return;
                    }
                }

                // Ignore any attempted changes to the estate owner's access level
                if (agentOrGroupID == estate.OwnerID)
                {
                    m_log.Warn("Ignoring " + flags + " estate access change for estate owner " + estate.OwnerID);
                    return;
                }

                if (flags.HasFlag(EstateAccessDeltaFlags.AllowedAgentAdd))
                    estate.AddUser(agentOrGroupID);
                if (flags.HasFlag(EstateAccessDeltaFlags.AllowedAgentRemove))
                    estate.RemoveUser(agentOrGroupID);
                if (flags.HasFlag(EstateAccessDeltaFlags.AllowedGroupAdd))
                    estate.AddGroup(agentOrGroupID);
                if (flags.HasFlag(EstateAccessDeltaFlags.AllowedGroupRemove))
                    estate.RemoveGroup(agentOrGroupID);
                if (flags.HasFlag(EstateAccessDeltaFlags.BannedAgentAdd))
                    estate.AddBannedUser(agentOrGroupID);
                if (flags.HasFlag(EstateAccessDeltaFlags.BannedAgentRemove))
                    estate.RemoveBannedUser(agentOrGroupID);
                if (flags.HasFlag(EstateAccessDeltaFlags.ManagerAdd))
                    estate.AddManager(agentOrGroupID);
                if (flags.HasFlag(EstateAccessDeltaFlags.ManagerRemove))
                    estate.RemoveManager(agentOrGroupID);

                // TODO: Handle EstateAccessDeltaFlags.ApplyToAllEstates and EstateAccessDeltaFlags.ApplyToManagedEstates

                if (!flags.HasFlag(EstateAccessDeltaFlags.NoReply))
                {
                    // Send a response
                    SendEstateAccessList(agent, estate, EstateAccessFlags.AllowedAgents | EstateAccessFlags.AllowedGroups |
                        EstateAccessFlags.BannedAgents | EstateAccessFlags.Managers, invoiceID);
                }
            }
            else
            {
                m_log.Warn(agent.Name + " sent an estateaccessdelta call with unrecognized parameters");
            }
        }

        #endregion Estate Owner Message Handler

        private void SendDetailedEstateData(LLAgent agent, Estate estate, UUID invoiceID)
        {
            string[] parameters = new string[]
            {
                estate.Name,
                estate.OwnerID.ToString(),
                estate.ID.ToString(),
                ((uint)estate.EstateFlags).ToString(),
                ((int)(estate.SunHour * 1024f)).ToString(),
                "1", // ParentEstateID
                estate.CovenantID.ToString(),
                estate.CovenantTimestamp.ToString(),
                "1", // SendToAgentOnly
                estate.AbuseEmail
            };

            SendEstateOwnerMessage(agent, "estateupdateinfo", parameters, invoiceID);
        }

        private void SendEstateAccessList(LLAgent agent, Estate estate, EstateAccessFlags flags, UUID invoiceID)
        {
            HashSet<UUID> agents = null;
            HashSet<UUID> groups = null;
            HashSet<UUID> bans = null;
            HashSet<UUID> managers = null;

            if ((flags & EstateAccessFlags.AllowedAgents) != 0)
                agents = estate.GetUsers();
            if ((flags & EstateAccessFlags.AllowedGroups) != 0)
                groups = estate.GetGroups();
            if ((flags & EstateAccessFlags.BannedAgents) != 0)
                bans = estate.GetBannedUsers();
            if ((flags & EstateAccessFlags.Managers) != 0)
                managers = estate.GetManagers();

            List<string> parameters = new List<string>()
            {
                estate.ID.ToString(),
                ((uint)flags).ToString(),
                (agents != null) ? agents.Count.ToString() : "0",
                (groups != null) ? groups.Count.ToString() : "0",
                (bans != null) ? bans.Count.ToString() : "0",
                (managers != null) ? managers.Count.ToString() : "0"
            };

            if (agents != null)
            {
                foreach (UUID id in agents)
                    parameters.Add(id.ToString());
            }
            if (groups != null)
            {
                foreach (UUID id in groups)
                    parameters.Add(id.ToString());
            }
            if (bans != null)
            {
                foreach (UUID id in bans)
                    parameters.Add(id.ToString());
            }
            if (managers != null)
            {
                foreach (UUID id in managers)
                    parameters.Add(id.ToString());
            }

            SendEstateOwnerMessage(agent, "setaccess", parameters.ToArray(), invoiceID);
        }

        private void SendEstateOwnerMessage(LLAgent agent, string method, string[] parameters, UUID invoiceID)
        {
            EstateOwnerMessagePacket message = new EstateOwnerMessagePacket();
            message.AgentData.AgentID = agent.ID;
            message.AgentData.TransactionID = UUID.Zero;
            message.MethodData.Method = Utils.StringToBytes(method);
            message.MethodData.Invoice = invoiceID;
            message.ParamList = new EstateOwnerMessagePacket.ParamListBlock[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                message.ParamList[i] = new EstateOwnerMessagePacket.ParamListBlock { Parameter = Utils.StringToBytes(parameters[i]) };

            m_udp.SendPacket(agent, message, ThrottleCategory.Task, false);
        }

        private void BroadcastRegionInfo()
        {
            m_scene.ForEachPresence(
                delegate(IScenePresence presence)
                {
                    if (presence is LLAgent)
                        RequestRegionInfoHandler(null, (LLAgent)presence);
                }
            );
        }

        private void BroadcastRegionHandshake()
        {
            m_scene.ForEachPresence(
                delegate(IScenePresence presence)
                {
                    if (presence is LLAgent)
                        SendRegionHandshake((LLAgent)presence, m_udp, m_scene, m_regionInfo, m_permissions);
                }
            );
        }

        public static void SendRegionHandshake(LLAgent agent, LLUDP udp, IScene scene, RegionInfo regionInfo, LLPermissions permissions)
        {
            RegionHandshakePacket handshake = new RegionHandshakePacket();

            // If the CacheID changes, the viewer will purge its object cache for this scene. We
            // just use the sceneID as the cacheID to make sure the viewer retains its object cache
            handshake.RegionInfo.CacheID = scene.ID;
            handshake.RegionInfo.SimName = Utils.StringToBytes(scene.Name);
            handshake.RegionInfo2.RegionID = scene.ID;
            handshake.RegionInfo.IsEstateManager = (permissions != null) ? permissions.IsEstateManager(agent) : true;
            handshake.RegionInfo3.ColoName = Utils.EmptyBytes;
            handshake.RegionInfo3.CPUClassID = 0;
            handshake.RegionInfo3.CPURatio = 0;

            if (regionInfo != null)
            {
                handshake.RegionInfo3.ProductName = Utils.StringToBytes(PRODUCT_NAME);
                handshake.RegionInfo3.ProductSKU = Utils.EmptyBytes;
                handshake.RegionInfo.RegionFlags = (uint)regionInfo.RegionFlags;
                handshake.RegionInfo.SimAccess = (byte)regionInfo.SimAccess;
                handshake.RegionInfo.SimOwner = regionInfo.OwnerID;
                handshake.RegionInfo.TerrainBase0 = UUID.Zero;
                handshake.RegionInfo.TerrainBase1 = UUID.Zero;
                handshake.RegionInfo.TerrainBase2 = UUID.Zero;
                handshake.RegionInfo.TerrainBase3 = UUID.Zero;
                handshake.RegionInfo.TerrainDetail0 = regionInfo.TerrainDetail0;
                handshake.RegionInfo.TerrainDetail1 = regionInfo.TerrainDetail1;
                handshake.RegionInfo.TerrainDetail2 = regionInfo.TerrainDetail2;
                handshake.RegionInfo.TerrainDetail3 = regionInfo.TerrainDetail3;
                handshake.RegionInfo.TerrainHeightRange00 = regionInfo.TerrainHeightRange00;
                handshake.RegionInfo.TerrainHeightRange01 = regionInfo.TerrainHeightRange01;
                handshake.RegionInfo.TerrainHeightRange10 = regionInfo.TerrainHeightRange10;
                handshake.RegionInfo.TerrainHeightRange11 = regionInfo.TerrainHeightRange11;
                handshake.RegionInfo.TerrainStartHeight00 = regionInfo.TerrainStartHeight00;
                handshake.RegionInfo.TerrainStartHeight01 = regionInfo.TerrainStartHeight01;
                handshake.RegionInfo.TerrainStartHeight10 = regionInfo.TerrainStartHeight10;
                handshake.RegionInfo.TerrainStartHeight11 = regionInfo.TerrainStartHeight11;
                handshake.RegionInfo.WaterHeight = regionInfo.WaterHeight;
            }
            else
            {
                handshake.RegionInfo3.ProductName = Utils.EmptyBytes;
                handshake.RegionInfo3.ProductSKU = Utils.EmptyBytes;
                handshake.RegionInfo.SimAccess = (byte)SimAccess.PG;
            }

            udp.SendPacket(agent, handshake, ThrottleCategory.Task, false);
        }

        private static bool ParamStringToBool(string s)
        {
            s = s.ToLowerInvariant();
            return (s == "1" || s == "y" || s == "yes" || s == "t" || s == "true");
        }

        private static RegionFlags ToggleRegionFlag(RegionFlags currentFlags, RegionFlags flag, bool enable)
        {
            return (enable)
                ? currentFlags | flag
                : currentFlags & ~flag;
        }
    }
}
