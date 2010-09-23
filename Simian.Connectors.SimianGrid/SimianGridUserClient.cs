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
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;

namespace Simian.Connectors.Standalone
{
    [ApplicationModule("SimianGridUserClient")]
    public class SimianGridUserClient : IUserClient, IApplicationModule
    {
        private const double CACHE_TIMEOUT = 60.0d * 5.0d;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private string m_serverUrl;

        private FileDataStore m_fileDataStore;
        private ExpiringCache<UUID, User> m_userCache = new ExpiringCache<UUID, User>();

        public bool Start(Simian simian)
        {
            IConfigSource source = simian.Config;
            IConfig config = source.Configs["SimianGrid"];
            if (config != null)
                m_serverUrl = config.GetString("UserService", null);

            if (String.IsNullOrEmpty(m_serverUrl))
            {
                m_log.Error("[SimianGrid] config section is missing the UserService URL");
                return false;
            }

            m_fileDataStore = simian.GetAppModule<FileDataStore>();

            return true;
        }

        public void Stop()
        {
        }

        #region IUserClient

        public bool CreateUser(string name, string email, byte accessLevel, OSDMap extradata, out User user)
        {
            user = new User
            {
                ID = new UUID(Utils.MD5String(name)),
                Name = name,
                Email = email,
                AccessLevel = accessLevel
            };
            if (extradata != null)
            {
                foreach (KeyValuePair<string, OSD> kvp in extradata)
                    user.SetField(kvp.Key, kvp.Value);
            }

            return CreateUser(user);
        }

        public bool CreateUser(User user)
        {
            m_log.Info("Creating user account for " + user.Name + " (" + user.ID + ")");

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddUser" },
                { "UserID", user.ID.ToString() },
                { "Name", user.Name },
                { "Email", user.Email },
                { "AccessLevel", user.AccessLevel.ToString() }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);

            if (response["Success"].AsBoolean())
            {
                // Cache the user account info
                m_userCache.AddOrUpdate(user.ID, user, CACHE_TIMEOUT);
                return true;
            }
            else
            {
                m_log.Warn("Failed to store user account for " + user.Name + ": " + response["Message"].AsString());
            }

            return false;
        }

        public bool UpdateUserFields(UUID userID, OSDMap fields)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddUserData" },
                { "UserID", userID.ToString() }
            };
            foreach (KeyValuePair<string, OSD> kvp in fields)
                requestArgs[kvp.Key] = OSDParser.SerializeJsonString(kvp.Value);

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (success)
            {
                // Update the cache
                User user;
                if (m_userCache.TryGetValue(userID, out user))
                {
                    foreach (KeyValuePair<string, OSD> kvp in fields)
                        user.SetField(kvp.Key, kvp.Value);
                }
            }
            else
            {
                m_log.Warn("Failed saving user data for " + userID + ": " + response["Message"].AsString());
            }

            return success;
        }

        public bool TryGetUser(UUID userID, out User user)
        {
            // Cache check
            if (m_userCache.TryGetValue(userID, out user))
                return true;

            // Remote request
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetUser" },
                { "UserID", userID.ToString() }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["User"] is OSDMap)
            {
                user = new User((OSDMap)response["User"]);

                // Cache the response
                m_userCache.AddOrUpdate(userID, user, CACHE_TIMEOUT);

                return true;
            }
            else
            {
                m_log.Warn("Failed to fetch user data for " + userID + ": " + response["Message"].AsString());
                user = null;
                return false;
            }
        }

        public User[] SearchUsers(string query)
        {
            List<User> users = new List<User>();

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetUsers" },
                { "NameQuery", query }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean())
            {
                OSDArray array = response["Users"] as OSDArray;
                if (array != null && array.Count > 0)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        // NOTE: These responses are not cached since they are likely only 
                        // relevant to this one-off search
                        if (array[i] is OSDMap)
                            users.Add(new User((OSDMap)array[i]));
                    }
                }
                else
                {
                    m_log.Warn("Account search failed, response data was in an invalid format");
                }
            }
            else
            {
                m_log.Warn("Failed to search for account data by name " + query + ": " + response["Message"].AsString());
            }

            return users.ToArray();
        }

        public bool TryAuthorizeIdentity(string identifier, string type, string credential, out User user)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AuthorizeIdentity" },
                { "Identifier", identifier },
                { "Credential", credential },
                { "Type", type }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean())
            {
                return TryGetUser(response["UserID"].AsUUID(), out user);
            }
            else
            {
                m_log.Warn("Failed to authorize identity " + identifier + " (" + type + "): " + response["Message"].AsString());
                user = null;
                return false;
            }
        }

        public bool CreateIdentity(Identity identity)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddIdentity" },
                { "Identifier", identity.Identifier },
                { "Credential", identity.Credential },
                { "Type", identity.Type },
                { "UserID", identity.UserID.ToString() },
                { "Enabled", identity.Enabled ? "1" : "0" }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("Failed to create identity " + identity.Identifier + " (" + identity.Type + "): " + response["Message"].AsString());

            return success;
        }

        public bool DeleteIdentity(string identity, string type)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "RemoveIdentity" },
                { "Identifier", identity },
                { "Type", type }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("Failed to delete identity " + identity + " (" + type + "): " + response["Message"].AsString());

            return success;
        }

        public Identity[] GetUserIdentities(UUID userID)
        {
            List<Identity> identities = new List<Identity>();

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetIdentities" },
                { "UserID", userID.ToString() }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["Identities"] is OSDArray)
            {
                OSDArray array = (OSDArray)response["Identities"];
                for (int i = 0; i < array.Count; i++)
                {
                    OSDMap map = array[i] as OSDMap;
                    if (map != null)
                    {
                        Identity identity = new Identity
                        {
                            Credential = map["Credential"].AsString(),
                            Enabled = map["Enabled"].AsBoolean(),
                            Identifier = map["Identifier"].AsString(),
                            Type = map["Type"].AsString(),
                            UserID = map["UserID"].AsUUID()
                        };
                        identities.Add(identity);
                    }
                }
            }
            else
            {
                m_log.Warn("Failed to retrieve identities for " + userID + ": " + response["Message"].AsString());
            }

            return identities.ToArray();
        }

        public bool AddSession(UserSession session)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddSession" },
                { "UserID", session.User.ID.ToString() },
                { "SessionID", session.SessionID.ToString() },
                { "SecureSessionID", session.SecureSessionID.ToString() }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("Failed to add session for " + session.User.ID + ": " + response["Message"].AsString());

            return success;
        }

        public bool UpdateSession(UserSession session)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "UpdateSession" },
                { "SessionID", session.SessionID.ToString() },
                { "SecureSessionID", session.SecureSessionID.ToString() },
                { "SceneID", session.CurrentSceneID.ToString() },
                { "ScenePosition", session.CurrentPosition.ToString() },
                { "SceneLookAt", session.CurrentLookAt.ToString() }
            };
            if (session.ExtraData != null)
                requestArgs.Add("ExtraData", OSDParser.SerializeJsonString(session.ExtraData));

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("Failed to update session for " + session.User.ID + ": " + response["Message"].AsString());

            return success;
        }

        public bool RemoveSession(UserSession session)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "RemoveSession" },
                { "SessionID", session.SessionID.ToString() },
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            // Store the user's last location if the logout succeeds
            if (success)
                UpdateUserFields(session.User.ID, new OSDMap { { "LastLocation", OSD.FromString(SerializeLocation(session.CurrentSceneID, session.CurrentPosition, session.CurrentLookAt)) } });
            else
                m_log.Warn("Failed to remove session for " + session.User.Name + ": " + response["Message"].AsString());

            return success;
        }

        public bool TryGetSession(UUID sessionID, out UserSession session)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetSession" },
                { "SessionID", sessionID.ToString() },
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean())
            {
                User user;
                if (TryGetUser(response["UserID"].AsUUID(), out user))
                {
                    session = new UserSession(user)
                    {
                        SessionID = response["SessionID"].AsUUID(),
                        SecureSessionID = response["SecureSessionID"].AsUUID(),
                        CurrentLookAt = response["SceneLookAt"].AsVector3(),
                        CurrentPosition = response["ScenePosition"].AsVector3(),
                        CurrentSceneID = response["SceneID"].AsUUID(),
                        ExtraData = response["ExtraData"] as OSDMap
                    };
                    return true;
                }
                else
                {
                    m_log.Warn("Session " + sessionID + " retrieved but failed to fetch the user, returning failure");
                }
            }
            else
            {
                m_log.Warn("Failed to retrieve session " + sessionID + ": " + response["Message"].AsString());
            }

            session = null;
            return false;
        }

        public bool TryGetSessionByUserID(UUID userID, out UserSession session)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetSession" },
                { "UserID", userID.ToString() },
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean())
            {
                User user;
                if (TryGetUser(userID, out user))
                {
                    session = new UserSession(user)
                    {
                        SessionID = response["SessionID"].AsUUID(),
                        SecureSessionID = response["SecureSessionID"].AsUUID(),
                        CurrentLookAt = response["SceneLookAt"].AsVector3(),
                        CurrentPosition = response["ScenePosition"].AsVector3(),
                        CurrentSceneID = response["SceneID"].AsUUID(),
                        ExtraData = response["ExtraData"] as OSDMap
                    };
                    return true;
                }
                else
                {
                    m_log.Warn("Session for " + userID + " retrieved but failed to fetch the user, returning failure");
                }
            }
            else
            {
                m_log.Warn("Failed to retrieve session for " + userID + ": " + response["Message"].AsString());
            }

            session = null;
            return false;
        }

        public bool TryGetFriends(UUID agentID, out IEnumerable<UUID> friends)
        {
            // FIXME:
            friends = null;
            return false;
        }

        #endregion IUserClient

        private string SerializeLocation(UUID regionID, Vector3 position, Vector3 lookAt)
        {
            return "{" + String.Format("\"SceneID\":\"{0}\",\"Position\":\"{1}\",\"LookAt\":\"{2}\"", regionID, position, lookAt) + "}";
        }
    }
}
