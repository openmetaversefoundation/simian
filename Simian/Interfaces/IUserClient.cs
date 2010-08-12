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
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian
{
    /// <summary>
    /// Represents a user account for this world. This class holds account and 
    /// presence details only, it does not store any authorization credentials
    /// </summary>
    /// <remarks>This class holds both user account information and current
    /// presence information for that account</remarks>
    public class User
    {
        private OSDMap m_data = new OSDMap();

        /// <summary>UUID of this user account</summary>
        public UUID ID
        {
            get { return m_data["id"].AsUUID(); }
            set { m_data["id"] = OSD.FromUUID(value); }
        }
        /// <summary>Current SessionID if the user is logged in, otherwise
        /// UUID.Zero</summary>
        public UUID SessionID
        {
            get { return m_data["session_id"].AsUUID(); }
            set { m_data["session_id"] = OSD.FromUUID(value); }
        }
        /// <summary>Current SecureSessionID if the user is logged in, otherwise
        /// UUID.Zero</summary>
        public UUID SecureSessionID
        {
            get { return m_data["secure_session_id"].AsUUID(); }
            set { m_data["secure_session_id"] = OSD.FromUUID(value); }
        }
        /// <summary>Full user name</summary>
        public string Name
        {
            get { return m_data["name"].AsString(); }
            set { m_data["name"] = OSD.FromString(value); }
        }
        /// <summary>Access level of the user in the current world, represented
        /// as a 0-255 value. 200 and above is considered an administrator, and
        /// a value of zero represents a foreign, anonymous, or untrusted 
        /// account</summary>
        public byte AccessLevel
        {
            get { return (byte)m_data["access_level"].AsInteger(); }
            set { m_data["access_level"] = OSD.FromInteger(value); }
        }
        /// <summary>Date and time of the last successful login</summary>
        public DateTime LastLogin
        {
            get { return m_data["last_login"].AsDate(); }
            set { m_data["last_login"] = OSD.FromDate(value); }
        }
        /// <summary>The home scene selected by this user, if null (UUID.Zero)
        /// HomePosition will be in global coordinates. If not null HomePosition
        /// will be in scene local coordinates</summary>
        public UUID HomeLocation
        {
            get { return m_data["home_location"].AsUUID(); }
            set { m_data["home_location"] = OSD.FromUUID(value); }
        }
        /// <summary>Home position</summary>
        public Vector3d HomePosition
        {
            get { return m_data["home_position"].AsVector3d(); }
            set { m_data["home_position"] = OSD.FromVector3d(value); }
        }
        /// <summary>Normalized looking direction vector for the home position</summary>
        public Vector3 HomeLookAt
        {
            get { return m_data["home_look_at"].AsVector3(); }
            set { m_data["home_look_at"] = OSD.FromVector3(value); }
        }
        /// <summary>The last scene this user was in, if null (UUID.Zero)
        /// LastPosition will be in global coordinates. If not null LastPosition
        /// will be in scene local coordinates</summary>
        public UUID LastLocation
        {
            get { return m_data["last_location"].AsUUID(); }
            set { m_data["last_location"] = OSD.FromUUID(value); }
        }
        /// <summary>Last position this user was reported at</summary>
        public Vector3d LastPosition
        {
            get { return m_data["last_position"].AsVector3d(); }
            set { m_data["last_position"] = OSD.FromVector3d(value); }
        }
        /// <summary>Normalized looking direction vector for the last position</summary>
        public Vector3 LastLookAt
        {
            get { return m_data["last_look_at"].AsVector3(); }
            set { m_data["last_look_at"] = OSD.FromVector3(value); }
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
        bool CreateUser(string name, byte accessLevel, OSDMap extradata, out User user);
        bool CreateUser(UUID id, string name, byte accessLevel, UUID homeLocation, Vector3d homePosition, Vector3 homeLookAt, OSDMap extraData, out User user);
        bool UpdateUser(User user);
        bool UpdateUserField(UUID userID, string field, OSD value);
        bool TryGetUser(UUID userID, out User user);
        User[] SearchUsers(string query);

        bool TryAuthorizeIdentity(string identifier, string type, string credential, out User user);

        bool CreateIdentity(Identity identity);
        bool DeleteIdentity(string identity);
        Identity[] GetUserIdentities(UUID userID);
    }
}
