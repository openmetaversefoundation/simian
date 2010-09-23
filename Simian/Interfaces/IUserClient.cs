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
using OpenMetaverse.StructuredData;

namespace Simian
{
    /// <summary>
    /// Represents a user account for this world. This class holds account 
    /// details only, it does not store any authorization credentials or 
    /// presence information
    /// </summary>
    public class User
    {
        private OSDMap m_data;

        #region Common User Data

        /// <summary>UUID of this user account</summary>
        public UUID ID
        {
            get { return m_data["UserID"].AsUUID(); }
            set { m_data["UserID"] = OSD.FromUUID(value); }
        }
        /// <summary>Full user name</summary>
        public string Name
        {
            get { return m_data["Name"].AsString(); }
            set { m_data["Name"] = OSD.FromString(value); }
        }
        /// <summary>User e-mail address</summary>
        public string Email
        {
            get { return m_data["Email"].AsString(); }
            set { m_data["Email"] = OSD.FromString(value); }
        }
        /// <summary>Access level of the user in the current world, represented
        /// as a 0-255 value. 200 and above is considered an administrator, and
        /// a value of zero represents a foreign, anonymous, or untrusted 
        /// account</summary>
        public byte AccessLevel
        {
            get { return (byte)m_data["AccessLevel"].AsInteger(); }
            set { m_data["AccessLevel"] = OSD.FromInteger(value); }
        }
        /// <summary>Date and time of the last successful login</summary>
        public DateTime LastLogin
        {
            get { return m_data["LastLoginDate"].AsDate(); }
            set { m_data["LastLoginDate"] = OSD.FromDate(value); }
        }
        /// <summary>UUID of the home region</summary>
        public UUID HomeSceneID
        {
            get { return m_data["HomeSceneID"].AsUUID(); }
            set { m_data["HomeSceneID"] = OSD.FromUUID(value); }
        }
        /// <summary>Home position, relative to the home scene</summary>
        public Vector3 HomePosition
        {
            get { return m_data["HomePosition"].AsVector3(); }
            set { m_data["HomePosition"] = OSD.FromVector3(value); }
        }
        /// <summary>Normalized looking direction vector for the home position</summary>
        public Vector3 HomeLookAt
        {
            get { return m_data["HomeLookAt"].AsVector3(); }
            set { m_data["HomeLookAt"] = OSD.FromVector3(value); }
        }
        /// <summary>UUID of the last region</summary>
        public UUID LastSceneID
        {
            get { return m_data["LastSceneID"].AsUUID(); }
            set { m_data["LastSceneID"] = OSD.FromUUID(value); }
        }
        /// <summary>Last scene-relative position this user was reported at</summary>
        public Vector3 LastPosition
        {
            get { return m_data["LastPosition"].AsVector3(); }
            set { m_data["LastPosition"] = OSD.FromVector3(value); }
        }
        /// <summary>Normalized looking direction vector for the last position</summary>
        public Vector3 LastLookAt
        {
            get { return m_data["LastLookAt"].AsVector3(); }
            set { m_data["LastLookAt"] = OSD.FromVector3(value); }
        }

        #endregion Common User Data

        public User()
        {
            m_data = new OSDMap();
        }

        public User(OSDMap user)
        {
            m_data = user;
        }

        public OSD GetField(string fieldName)
        {
            return m_data[fieldName];
        }

        public void SetField(string fieldName, OSD value)
        {
            m_data[fieldName] = value;
        }

        public OSDMap GetOSD()
        {
            return m_data;
        }

        public static User FromOSD(OSDMap map)
        {
            User user = new User();
            user.m_data = map;
            return user;
        }
    }

    public class UserSession
    {
        public readonly User User;
        public UUID SessionID;
        public UUID SecureSessionID;
        public UUID CurrentSceneID;
        public Vector3 CurrentPosition;
        public Vector3 CurrentLookAt;
        public OSDMap ExtraData;

        public UserSession(User user)
        {
            User = user;
            ExtraData = new OSDMap();
        }

        public OSD GetField(string fieldName)
        {
            return ExtraData[fieldName];
        }

        public void SetField(string fieldName, OSD value)
        {
            ExtraData[fieldName] = value;
        }
    }

    /// <summary>
    /// Holds identity information and credentials for an authorization method.
    /// There is a many to one mapping of identities to user accounts
    /// </summary>
    /// <remarks>There is no API to remove a user account since objects in the
    /// virtual world may rely on the existence of a user account. Instead,
    /// logins for an account can be disabled by deleting all of the identities
    /// for an account or making them as disabled</remarks>
    public class Identity
    {
        /// <summary>Identitifer for this identity. This can be a
        /// name, a URL, or any other form of identifier</summary>
        public string Identifier;
        /// <summary>The type of identity. This dictates which module will
        /// handle authorization for this identity</summary>
        public string Type;
        /// <summary>Optional data store for the current type of authorization.
        /// This could be a salted password hash, for example</summary>
        public string Credential;
        /// <summary>True if this identity is enabled, otherwise false</summary>
        public bool Enabled;
        /// <summary>UUID of the user this identity links to</summary>
        public UUID UserID;

        public override string ToString()
        {
            return "[" + Type + "] " + Identifier + " -> " + UserID + " (" + (Enabled ? "Enabled" : "Disabled") + ")";
        }
    }

    public interface IUserClient
    {
        bool CreateUser(string name, string email, byte accessLevel, OSDMap extradata, out User user);
        bool CreateUser(User user);
        bool UpdateUserFields(UUID userID, OSDMap fields);
        bool TryGetUser(UUID userID, out User user);
        User[] SearchUsers(string query);

        bool TryAuthorizeIdentity(string identifier, string type, string credential, out User user);
        bool CreateIdentity(Identity identity);
        bool DeleteIdentity(string identity, string type);
        Identity[] GetUserIdentities(UUID userID);

        bool AddSession(UserSession session);
        bool UpdateSession(UserSession session);
        bool RemoveSession(UserSession session);
        bool TryGetSession(UUID sessionID, out UserSession session);
        bool TryGetSessionByUserID(UUID userID, out UserSession session);

        bool TryGetFriends(UUID agentID, out IEnumerable<UUID> friends);
    }
}
