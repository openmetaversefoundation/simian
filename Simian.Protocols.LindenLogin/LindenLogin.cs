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
                    User agent = AnonymousLogin(name, 200, null, name, AUTH_METHOD, passHash);
                    bool authorized = true;

                    if (authorized)
                    {
                        // Initialize presence data for this user
                        int circuitCode = m_circuitCodeGenerator.Next();
                        agent.SessionID = UUID.Random();
                        agent.SecureSessionID = UUID.Random();
                        agent.SetField("circuit_code", OSD.FromInteger(circuitCode));
                        agent.LastLogin = DateTime.UtcNow;

                        // Find a scene that this user is authorized to login to
                        Vector3 startPosition, lookAt;
                        SceneInfo sceneInfo;
                        IPAddress address;
                        int port;
                        Uri seedCap;

                        if (TryGetLoginScene(agent, ref startLocation, out sceneInfo, out startPosition, out lookAt, out address, out port, out seedCap))
                        {
                            m_log.Debug("Authenticated " + agent.Name);

                            // Update the user with the current presence data
                            m_userClient.UpdateUser(agent);

                            #region Login Success Response

                            // Session is created, construct the login response
                            LindenLoginData response = new LindenLoginData();

                            uint regionX, regionY;
                            GetRegionXY(sceneInfo.MinPosition, out regionX, out regionY);

                            uint homeRegionX, homeRegionY;
                            GetRegionXY(agent.HomePosition, out homeRegionX, out homeRegionY);

                            Vector3d homeMinPosition = new Vector3d(homeRegionX * 256.0d, homeRegionY * 256.0d, 0.0d);
                            Vector3d homePosition = agent.HomePosition - homeMinPosition;

                            response.AgentID = agent.ID;
                            response.BuddyList = GetBuddyList(agent.ID);
                            response.CircuitCode = circuitCode;
                            SetClassifiedCategories(ref response);
                            response.FirstName = firstName;
                            response.HomeLookAt = agent.HomeLookAt;
                            response.HomePosition = new Vector3(homePosition);
                            response.HomeRegionX = homeRegionX;
                            response.HomeRegionY = homeRegionY;
                            response.LastName = lastName;
                            response.Login = true;
                            response.LookAt = lookAt;
                            response.Message = "DEFAULT_WELCOME_MESSAGE";
                            response.RegionX = regionX;
                            response.RegionY = regionY;
                            response.SeedCapability = (seedCap != null) ? seedCap.AbsoluteUri : "http://localhost:0/";
                            response.SessionID = agent.SessionID;
                            response.SecureSessionID = agent.SecureSessionID;
                            response.StartLocation = startLocation;
                            response.SimAddress = address.ToString();
                            response.SimPort = (uint)port;

                            SetActiveGestures(agent, ref response);

                            GetInventory(agent, ref response);

                            m_log.Info("Login to " + sceneInfo.Name + " prepared for " + agent.Name + ", returning response");
                            return response.ToXmlRpcResponse();

                            //return CreateLoginInternalErrorResponse();

                            #endregion Login Success Response
                        }
                        else
                        {
                            m_log.Error("Could not find a default local scene, cancelling login");
                            return CreateLoginNoRegionResponse();
                        }
                    }
                    else
                    {
                        return CreateLoginFailedResponse();
                    }
                }
            }

            return CreateLoginGridErrorResponse();
        }

        private User AnonymousLogin(string name, byte accessLevel, OSDMap extraData, string identifier, string type, string credential)
        {
            User user;
            if (m_userClient.TryAuthorizeIdentity(identifier, type, credential, out user))
                return user;

            m_userClient.CreateUser(name, accessLevel, extraData, out user);
            m_userClient.CreateIdentity(new Identity { UserID = user.ID, Enabled = true, Credential = credential, Identifier = identifier, Type = type });

            return user;
        }

        private bool TryGetLoginScene(User agent, ref string startLocation, out SceneInfo sceneInfo, out Vector3 sceneStartPosition,
            out Vector3 lookAt, out IPAddress address, out int port, out Uri seedCap)
        {
            sceneInfo = null;
            sceneStartPosition = Vector3.Zero;

            if (startLocation.Equals("last", StringComparison.InvariantCultureIgnoreCase))
            {
                // Try to get the scene nearest the last position of this agent
                m_gridClient.TryGetSceneNear(agent.LastPosition, true, out sceneInfo);
                sceneStartPosition = Util.PositionToLocalPosition(agent.LastPosition);
            }
            else if (startLocation.Equals("home", StringComparison.InvariantCultureIgnoreCase))
            {
                // Try to get the scene nearest the home position of this agent
                m_gridClient.TryGetSceneNear(agent.HomePosition, true, out sceneInfo);
                sceneStartPosition = Util.PositionToLocalPosition(agent.HomePosition);
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

            if (sceneInfo != null)
            {
                // Send a rez_avatar/request message to this scene
                OSDMap response = RezAvatarRequest(sceneInfo, agent, sceneStartPosition);

                if (response != null)
                {
                    sceneStartPosition = response["position"].AsVector3();
                    lookAt = response["look_at"].AsVector3();
                    IPAddress.TryParse(response["sim_host"].AsString(), out address);
                    port = response["sim_port"].AsInteger();
                    seedCap = response["region_seed_capability"].AsUri();

                    m_log.Debug("Found scene " + sceneInfo.Name + " for " + agent.Name + " to login to");
                    return true;
                }
                else if (startLocation.Equals("last", StringComparison.InvariantCultureIgnoreCase))
                {
                    m_log.Info("Could not create a presence for user " + agent.Name + " in last scene " +
                        sceneInfo.Name + ", trying home starting location");

                    startLocation = "home";
                    return TryGetLoginScene(agent, ref startLocation, out sceneInfo, out sceneStartPosition,
                        out lookAt, out address, out port, out seedCap);
                }
                else
                {
                    m_log.Info("Could not create a presence for user " + agent.Name + " in home scene " +
                        sceneInfo.Name + ", giving up");
                }
            }
            else
            {
                m_log.Warn("Could not find a starting location for " + agent.Name + " with requested location " + startLocation);
            }

            sceneStartPosition = Vector3.Zero;
            lookAt = Vector3.Zero;
            address = null;
            port = 0;
            seedCap = null;
            return false;
        }

        private OSDMap RezAvatarRequest(SceneInfo sceneInfo, User user, Vector3 relativeStartPosition)
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
                    Util.GetFirstLastName(user.Name, out firstName, out lastName);

                    OSDMap rezAvatarRequest = new OSDMap
                    {
                        { "agent_id", OSD.FromUUID(user.ID) },
                        { "circuit_code", user.GetField("circuit_code") },
                        { "secure_session_id", OSD.FromUUID(user.SecureSessionID) },
                        { "session_id", OSD.FromUUID(user.SessionID) },
                        { "first_name", OSD.FromString(firstName) },
                        { "last_name", OSD.FromString(lastName) },
                        { "position", OSD.FromVector3(relativeStartPosition) },
                        { "access_level", OSD.FromInteger(user.AccessLevel) }
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
                            m_log.Warn("Cannot rez avatar " + user.Name + ", rez_avatar/request to " + rezAvatarRequestCap + " failed: " + rezAvatarResponse["message"].AsString());
                    }
                }
                else
                {
                    m_log.Warn("Cannot rez avatar " + user.Name + ", rez_avatar/request capability not found in public region seed capability: " + publicRegionCaps.ToString());
                }
            }
            else
            {
                m_log.Warn("Cannot rez avatar " + user.Name + ", Failed to fetch public region seed capability from " + publicRegionSeedCap);
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
