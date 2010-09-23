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
using OpenMetaverse;

namespace Simian.Protocols.Linden
{
    public class Estate
    {
        public uint ID;
        public UUID OwnerID;
        public RegionFlags EstateFlags;
        public string Name = String.Empty;
        public string AbuseEmail = String.Empty;
        public SimAccess AccessFlags = SimAccess.PG;
        public float SunHour;
        public bool UseGlobalSun;
        public bool UseFixedSun;
        public float TerrainLowerLimit;
        public float TerrainRaiseLimit;
        public UUID CovenantID;
        public uint CovenantTimestamp;
        public uint MaxAgents;
        public ushort MatureLevel;
        public float ObjectBonusFactor;
        public UUID TerrainDetail0;
        public UUID TerrainDetail1;
        public UUID TerrainDetail2;
        public UUID TerrainDetail3;
        public float TerrainHeightRange00;
        public float TerrainHeightRange01;
        public float TerrainHeightRange10;
        public float TerrainHeightRange11;
        public float TerrainStartHeight00;
        public float TerrainStartHeight01;
        public float TerrainStartHeight10;
        public float TerrainStartHeight11;

        private HashSet<UUID> m_regionIDs = new HashSet<UUID>();
        private HashSet<UUID> m_groupIDs = new HashSet<UUID>();
        private HashSet<UUID> m_managerIDs = new HashSet<UUID>();
        private HashSet<UUID> m_userIDs = new HashSet<UUID>();
        private HashSet<UUID> m_bannedUserIDs = new HashSet<UUID>();

        private object m_syncRoot = new object();

        #region Collection Methods

        public bool ContainsRegion(UUID regionID)
        {
            return m_regionIDs.Contains(regionID);
        }

        public bool ContainsGroup(UUID groupID)
        {
            return m_groupIDs.Contains(groupID);
        }

        public bool ContainsManager(UUID managerID)
        {
            return m_managerIDs.Contains(managerID);
        }

        public bool ContainsUser(UUID userID)
        {
            return m_userIDs.Contains(userID);
        }

        public bool ContainsBannedUser(UUID userID)
        {
            return m_bannedUserIDs.Contains(userID);
        }

        public bool AddRegion(UUID regionID)
        {
            lock (m_syncRoot)
                return m_regionIDs.Add(regionID);
        }

        public bool AddGroup(UUID groupID)
        {
            lock (m_syncRoot)
                return m_groupIDs.Add(groupID);
        }

        public bool AddManager(UUID userID)
        {
            lock (m_syncRoot)
                return m_managerIDs.Add(userID);
        }

        public bool AddUser(UUID userID)
        {
            lock (m_syncRoot)
                return m_userIDs.Add(userID);
        }

        public bool AddBannedUser(UUID userID)
        {
            lock (m_syncRoot)
                return m_bannedUserIDs.Add(userID);
        }

        public bool RemoveRegion(UUID regionID)
        {
            lock (m_syncRoot)
                return m_regionIDs.Remove(regionID);
        }

        public bool RemoveGroup(UUID groupID)
        {
            lock (m_syncRoot)
                return m_groupIDs.Remove(groupID);
        }

        public bool RemoveManager(UUID userID)
        {
            lock (m_syncRoot)
                return m_managerIDs.Remove(userID);
        }

        public bool RemoveUser(UUID userID)
        {
            lock (m_syncRoot)
                return m_userIDs.Remove(userID);
        }

        public bool RemoveBannedUser(UUID userID)
        {
            lock (m_syncRoot)
                return m_bannedUserIDs.Remove(userID);
        }

        public HashSet<UUID> GetRegions()
        {
            lock (m_syncRoot)
                return new HashSet<UUID>(m_regionIDs);
        }

        public HashSet<UUID> GetGroups()
        {
            lock (m_syncRoot)
                return new HashSet<UUID>(m_groupIDs);
        }

        public HashSet<UUID> GetManagers()
        {
            lock (m_syncRoot)
                return new HashSet<UUID>(m_managerIDs);
        }

        public HashSet<UUID> GetUsers()
        {
            lock (m_syncRoot)
                return new HashSet<UUID>(m_userIDs);
        }

        public HashSet<UUID> GetBannedUsers()
        {
            lock (m_syncRoot)
                return new HashSet<UUID>(m_bannedUserIDs);
        }

        #endregion Collection Methods
    }

    public interface IEstateClient
    {
        bool AddOrUpdateEstate(Estate estate);
        bool RemoveEstate(int estateID);

        bool TryGetEstate(int estateID, out Estate estate);
        bool TryGetEstate(string estateName, out Estate estate);
        bool TryGetEstate(UUID sceneID, out Estate estate);

        bool JoinEstate(UUID sceneID, int estateID);
    }
}
