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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Simian.Protocols.Linden;
using log4net;
using HttpServer;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.LindenLogin
{
    [ApplicationModule("LindenLogin")]
    public class LindenLogin : IApplicationModule
    {
        private const string AUTH_METHOD = "md5hash";
        private const int INVENTORY_FETCH_TIMEOUT_MS = 1000 * 30;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private Simian m_simian;
        private IHttpServer m_httpServer;
        private IUserClient m_userClient;
        private IGridClient m_gridClient;
        private IInventoryClient m_inventoryClient;
        /// <summary>Generates new circuit codes</summary>
        Random m_circuitCodeGenerator = new Random();

        public bool Start(Simian simian)
        {
            m_simian = simian;

            #region Get Module References

            m_httpServer = simian.GetAppModule<IHttpServer>();
            if (m_httpServer == null)
            {
                m_log.Error("Can't create the LindenLogin service without an HTTP server");
                return false;
            }

            m_userClient = simian.GetAppModule<IUserClient>();
            if (m_userClient == null)
            {
                m_log.Error("Can't create the LindenLogin service without a user client");
                return false;
            }

            m_gridClient = simian.GetAppModule<IGridClient>();
            if (m_gridClient == null)
            {
                m_log.Error("Can't create the LindenLogin service without a grid client");
                return false;
            }

            m_inventoryClient = simian.GetAppModule<IInventoryClient>();

            #endregion Get Module References

            m_httpServer.AddXmlRpcHandler("/", true, "login_to_simulator", LoginHandler);
            m_log.Info("LindenLogin handler initialized");

            return true;
        }

        public void Stop()
        {
            if (m_httpServer != null)
                m_httpServer.RemoveXmlRpcHandlers("login_to_simulator");
        }

        private XmlRpcResponse LoginHandler(XmlRpcRequest request, IHttpRequest httpRequest)
        {
            Hashtable requestData = (Hashtable)request.Params[0];

            bool validLogin = requestData.ContainsKey("first") && requestData.ContainsKey("last") &&
                (requestData.ContainsKey("passwd") || requestData.Contains("web_login_key"));

            if (validLogin)
            {
                string startLocation = (requestData.ContainsKey("start") ? (string)requestData["start"] : "last");
                string version = (requestData.ContainsKey("version") ? (string)requestData["version"] : "Unknown");
                string firstName = (string)requestData["first"];
                string lastName = (string)requestData["last"];
                string passHash = (string)requestData["passwd"];

                m_log.InfoFormat("Received XML-RPC login request for {0} {1} with client \"{2}\" to destination \"{3}\"",
                    firstName, lastName, version, startLocation);

                if (!String.IsNullOrEmpty(passHash))
                {
                    // Try to login
                    string name = firstName + ' ' + lastName;
                    
                    // DEBUG: Anonymous logins are always enabled
                    UserSession session = AnonymousLogin(name, 200, null, name, AUTH_METHOD, passHash);

                    SceneInfo loginScene;
                    Vector3 startPosition, lookAt;
                    IPAddress address;
                    int port;
                    Uri seedCap;

                    // Find a scene that this user is authorized to login to
                    if (TryGetLoginScene(session, ref startLocation, out loginScene, out startPosition, out lookAt, out address, out port, out seedCap))
                    {
                        m_log.Debug("Authenticated " + session.User.Name);

                        #region Login Success Response

                        // Session is created, construct the login response
                        LindenLoginData response = new LindenLoginData();

                        uint regionX, regionY;
                        GetRegionXY(loginScene.MinPosition, out regionX, out regionY);

                        response.AgentID = session.User.ID;
                        response.BuddyList = GetBuddyList(session.User.ID);
                        response.CircuitCode = session.GetField("CircuitCode").AsInteger();
                        SetClassifiedCategories(ref response);
                        response.FirstName = firstName;
                        
                        response.LastName = lastName;
                        response.Login = true;
                        response.LookAt = lookAt;
                        response.Message = "Welcome to Simian";
                        response.RegionX = regionX;
                        response.RegionY = regionY;
                        response.SeedCapability = (seedCap != null) ? seedCap.AbsoluteUri : "http://localhost:0/";
                        response.SessionID = session.SessionID;
                        response.SecureSessionID = session.SecureSessionID;
                        response.StartLocation = startLocation;
                        response.SimAddress = address.ToString();
                        response.SimPort = (uint)port;

                        // Set the home scene information
                        SceneInfo homeScene;
                        if (m_gridClient.TryGetScene(session.User.HomeSceneID, out homeScene))
                        {
                            uint homeRegionX, homeRegionY;
                            GetRegionXY(homeScene.MinPosition, out homeRegionX, out homeRegionY);

                            response.HomeLookAt = session.User.HomeLookAt;
                            response.HomePosition = session.User.HomePosition;
                            response.HomeRegionX = homeRegionX;
                            response.HomeRegionY = homeRegionY;
                        }
                        else
                        {
                            response.HomeLookAt = lookAt;
                            response.HomePosition = startPosition;
                            response.HomeRegionX = regionX;
                            response.HomeRegionY = regionY;
                        }

                        SetActiveGestures(session.User, ref response);

                        GetInventory(session.User, ref response);

                        m_log.Info("Login to " + loginScene.Name + " prepared for " + session.User.Name + ", returning response");
                        return response.ToXmlRpcResponse();

                        #endregion Login Success Response
                    }
                    else
                    {
                        m_log.Error("Could not find a default local scene for " + name + ", cancelling login");
                        m_userClient.RemoveSession(session);
                        return CreateLoginNoRegionResponse();
                    }
                }
            }

            m_log.Warn("Received invalid login data, returning an error response");
            return CreateLoginGridErrorResponse();
        }

        private UserSession AnonymousLogin(string name, byte accessLevel, OSDMap extraData, string identifier, string type, string credential)
        {
            User user;
            if (!m_userClient.TryAuthorizeIdentity(identifier, type, credential, out user))
            {
                // We don't have an e-mail address for this person so just create a random string for the e-mail
                string email = "INVALID " + UUID.Random().ToString();

                // Create a new user and identity
                m_userClient.CreateUser(name, email, accessLevel, extraData, out user);
                m_userClient.CreateIdentity(new Identity { UserID = user.ID, Enabled = true, Credential = credential, Identifier = identifier, Type = type });
            }

            // Create a session for this user
            UserSession session = new UserSession(user);
            session.SessionID = UUID.Random();
            session.SecureSessionID = UUID.Random();
            session.SetField("CircuitCode", OSD.FromInteger(m_circuitCodeGenerator.Next()));
            user.LastLogin = DateTime.UtcNow;

            // Store the session in the user service
            m_userClient.AddSession(session);

            return session;
        }

        private bool TryGetLoginScene(UserSession session, ref string startLocation, out SceneInfo sceneInfo, out Vector3 sceneStartPosition,
            out Vector3 lookAt, out IPAddress address, out int port, out Uri seedCap)
        {
            sceneInfo = null;
            sceneStartPosition = Vector3.Zero;
            lookAt = Vector3.UnitY;

            if (startLocation.Equals("last", StringComparison.InvariantCultureIgnoreCase))
            {
                m_gridClient.TryGetScene(session.User.LastSceneID, out sceneInfo);
                sceneStartPosition = session.User.LastPosition;
            }
            else if (startLocation.Equals("home", StringComparison.InvariantCultureIgnoreCase))
            {
                m_gridClient.TryGetScene(session.User.HomeSceneID, out sceneInfo);
                sceneStartPosition = session.User.HomePosition;
            }
            else
            {
                // Parse the start location into a search query
                Regex regex = new Regex(@"^uri:(?<region>[^&]+)&(?<x>\d+)&(?<y>\d+)&(?<z>\d+)$");
                Match uriMatch = regex.Match(startLocation);

                if (uriMatch != null)
                {
                    // Use our search query through the grid client to find a match
                    string region = uriMatch.Groups["region"].Value;
                    SceneInfo[] results = m_gridClient.SearchScenes(region, 1, true);

                    if (results != null && results.Length > 0)
                    {
                        sceneInfo = results[0];

                        float x, y, z;
                        Single.TryParse(uriMatch.Groups["x"].Value, out x);
                        Single.TryParse(uriMatch.Groups["y"].Value, out y);
                        Single.TryParse(uriMatch.Groups["z"].Value, out z);
                        sceneStartPosition = new Vector3(x, y, z);
                    }
                }
            }

            // Try to find any valid region to start in
            if (sceneInfo == null)
            {
                m_gridClient.TryGetSceneNear(Vector3d.Zero, true, out sceneInfo);
                if (sceneInfo != null)
                {
                    sceneStartPosition = new Vector3((sceneInfo.MinPosition + sceneInfo.MaxPosition) * 0.5d - sceneInfo.MinPosition);
                    sceneStartPosition.Z = 0f;
                }
            }

            if (sceneInfo != null)
            {
                // Send a rez_avatar/request message to this scene
                OSDMap response = RezAvatarRequest(sceneInfo, session, sceneStartPosition, lookAt);

                if (response != null)
                {
                    sceneStartPosition = response["position"].AsVector3();
                    lookAt = response["look_at"].AsVector3();
                    IPAddress.TryParse(response["sim_host"].AsString(), out address);
                    port = response["sim_port"].AsInteger();
                    seedCap = response["region_seed_capability"].AsUri();

                    m_log.Debug("Found scene " + sceneInfo.Name + " for " + session.User.Name + " to login to");
                    return true;
                }
                else if (startLocation.Equals("last", StringComparison.InvariantCultureIgnoreCase))
                {
                    m_log.Info("Could not create a presence for user " + session.User.Name + " in last scene " +
                        sceneInfo.Name + ", trying home starting location");

                    startLocation = "home";
                    return TryGetLoginScene(session, ref startLocation, out sceneInfo, out sceneStartPosition,
                        out lookAt, out address, out port, out seedCap);
                }
                else
                {
                    m_log.Info("Could not create a presence for user " + session.User.Name + " in home scene " +
                        sceneInfo.Name + ", giving up");
                }
            }
            else
            {
                m_log.Warn("Could not find a starting location for " + session.User.Name + " with requested location " + startLocation);
            }

            sceneStartPosition = Vector3.Zero;
            lookAt = Vector3.Zero;
            address = null;
            port = 0;
            seedCap = null;
            return false;
        }

        private OSDMap RezAvatarRequest(SceneInfo sceneInfo, UserSession session, Vector3 relativeStartPosition, Vector3 lookAt)
        {
            string urlFriendlySceneName = WebUtil.UrlEncode(sceneInfo.Name);
            Uri publicRegionSeedCap = sceneInfo.PublicSeedCapability;

            // Send a request to the public region seed cap
            OSDMap publicRegionCaps = null;
            try
            {
                publicRegionCaps = OSDParser.Deserialize(UntrustedHttpWebRequest.GetUntrustedUrl(publicRegionSeedCap)) as OSDMap;
                if (publicRegionCaps != null)
                    publicRegionCaps = publicRegionCaps["capabilities"] as OSDMap;
            }
            catch { }

            if (publicRegionCaps != null)
            {
                // Parse the rez_avatar/request cap out
                Uri rezAvatarRequestCap = publicRegionCaps["rez_avatar/request"].AsUri();

                if (rezAvatarRequestCap != null)
                {
                    string firstName, lastName;
                    Util.GetFirstLastName(session.User.Name, out firstName, out lastName);

                    OSDMap rezAvatarRequest = new OSDMap
                    {
                        { "agent_id", OSD.FromUUID(session.User.ID) },
                        { "session_id", OSD.FromUUID(session.SessionID) },
                        { "position", OSD.FromVector3(relativeStartPosition) },
                        { "look_at", OSD.FromVector3(lookAt) },
                        { "velocity", OSD.FromVector3(Vector3.Zero) },
                        { "child", OSD.FromBoolean(false) }
                    };

                    OSDMap rezAvatarResponse = null;
                    try
                    {
                        rezAvatarResponse = OSDParser.Deserialize(UntrustedHttpWebRequest.PostToUntrustedUrl(
                            rezAvatarRequestCap, OSDParser.SerializeLLSDXmlString(rezAvatarRequest))) as OSDMap;
                    }
                    catch { }

                    if (rezAvatarResponse != null)
                    {
                        // Parse the response data
                        if (rezAvatarResponse["connect"].AsBoolean())
                            return rezAvatarResponse;
                        else
                            m_log.Warn("Cannot rez avatar " + session.User.Name + ", rez_avatar/request to " + rezAvatarRequestCap + " failed: " + rezAvatarResponse["message"].AsString());
                    }
                }
                else
                {
                    m_log.Warn("Cannot rez avatar " + session.User.Name + ", rez_avatar/request capability not found in public region seed capability: " + publicRegionCaps.ToString());
                }
            }
            else
            {
                m_log.Warn("Cannot rez avatar " + session.User.Name + ", Failed to fetch public region seed capability from " + publicRegionSeedCap);
            }

            return null;
        }

        private void GetRegionXY(Vector3d position, out uint regionX, out uint regionY)
        {
            regionX = ((uint)position.X / 256u);
            regionY = ((uint)position.Y / 256u);
        }

        private Hashtable GetBuddyList(UUID avatarID)
        {
            // TODO: Buddy list support
            return null;
        }

        private void SetClassifiedCategories(ref LindenLoginData response)
        {
            response.AddClassifiedCategory(1, "Shopping");
            response.AddClassifiedCategory(2, "Land Rental");
            response.AddClassifiedCategory(3, "Property Rental");
            response.AddClassifiedCategory(4, "Special Attraction");
            response.AddClassifiedCategory(5, "New Products");
            response.AddClassifiedCategory(6, "Employment");
            response.AddClassifiedCategory(7, "Wanted");
            response.AddClassifiedCategory(8, "Service");
            response.AddClassifiedCategory(9, "Personal");
        }

        private void SetActiveGestures(User user, ref LindenLoginData response)
        {
            // TODO: Pull this information out of user.ExtraData
            //response.ActiveGestures
        }

        private void GetInventory(User user, ref LindenLoginData response)
        {
            Hashtable folderData;

            if (m_inventoryClient != null)
            {
                InventorySkeleton skeleton;
                if (m_inventoryClient.TryGetInventorySkeleton(user.ID, out skeleton))
                {
                    response.InventoryRoot = skeleton.RootFolderID;
                    response.AgentInventory = FillInventorySkeleton(skeleton.Skeleton);

                    response.InventoryLibRoot = skeleton.LibraryFolderID;
                    response.InventoryLibraryOwner = skeleton.LibraryOwner;
                    response.InventoryLibrary = FillInventorySkeleton(skeleton.LibrarySkeleton);

                    response.SetInitialOutfit("Default Outfit", false);
                    return;
                }
                else
                {
                    m_log.Error("Failed to fetch inventory for " + user.Name + ", returning a temporary root folder");
                }
            }

            m_log.Info("Returning a random root folder ID so login will succeed");

            response.InventoryRoot = UUID.Random();
            response.AgentInventory = new ArrayList(1);

            folderData = new Hashtable();
            folderData["name"] = "My Inventory";
            folderData["parent_id"] = UUID.Zero.ToString();
            folderData["type_default"] = (int)AssetType.Folder;
            folderData["folder_id"] = response.InventoryRoot.ToString();
            folderData["version"] = 1;

            response.AgentInventory.Add(folderData);

            response.SetInitialOutfit("Default Outfit", false);
        }

        private ArrayList FillInventorySkeleton(InventoryFolder rootFolder)
        {
            ArrayList inventory = new ArrayList(rootFolder.Children.Count + 1);

            Hashtable rootFolderData = new Hashtable();
            rootFolderData["name"] = rootFolder.Name;
            rootFolderData["parent_id"] = rootFolder.ParentID.ToString();
            rootFolderData["type_default"] = (int)LLUtil.ContentTypeToLLAssetType(rootFolder.PreferredContentType);
            rootFolderData["folder_id"] = rootFolder.ID.ToString();
            rootFolderData["version"] = rootFolder.Version;
            inventory.Add(rootFolderData);

            foreach (var child in rootFolder.Children.Values)
            {
                InventoryFolder folder = (InventoryFolder)child;
                Hashtable folderData = new Hashtable();
                folderData["name"] = folder.Name;
                folderData["parent_id"] = folder.ParentID.ToString();
                folderData["type_default"] = (int)LLUtil.ContentTypeToLLAssetType(folder.PreferredContentType);
                folderData["folder_id"] = folder.ID.ToString();
                folderData["version"] = folder.Version;

                inventory.Add(folderData);
            }
            
            return inventory;
        }

        #region Login XML responses

        public XmlRpcResponse CreateFailureResponse(string reason, string message, bool loginSuccess)
        {
            Hashtable responseData = new Hashtable(3);
            responseData["reason"] = reason;
            responseData["message"] = message;
            responseData["login"] = loginSuccess.ToString().ToLower();

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse CreateLoginFailedResponse()
        {
            return CreateFailureResponse(
                "key",
                "Could not authenticate your avatar. Please check your username and password, and check the grid if problems persist.",
                false);
        }

        public XmlRpcResponse CreateLoginGridErrorResponse()
        {
            return CreateFailureResponse(
                "key",
                "Error connecting to grid. Could not perceive credentials from login XML.",
                false);
        }

        public XmlRpcResponse CreateLoginBlockedResponse()
        {
            return CreateFailureResponse(
                "presence",
                "Logins are currently restricted. Please try again later",
                false);
        }

        public XmlRpcResponse CreateLoginInternalErrorResponse()
        {
            return CreateFailureResponse(
                "key",
                "The login server failed to complete the login process. Please try again later",
                false);
        }

        public XmlRpcResponse CreateLoginNoRegionResponse()
        {
            return CreateFailureResponse(
                "key",
                "The login server could not find an available region to login to. Please try again later",
                false);
        }

        #endregion Login XML responses
    }
}
