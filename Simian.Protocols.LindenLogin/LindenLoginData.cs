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
using OpenMetaverse;
using Nwc.XmlRpc;
using log4net;

namespace Simian.Protocols.LindenLogin
{
    public class LindenLoginData
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        #region Active parameters

        /// <summary>True if login succeeded, otherwise false</summary>
        public bool Login;
        /// <summary>UUID of the agent logging in</summary>
        public UUID AgentID;
        /// <summary>Temporary session cookie that the client uses to authenticate requests</summary>
        public UUID SessionID;
        /// <summary>Temporary shared secret that is sent to the client at login and not transmitted
        /// over the wire again</summary>
        public UUID SecureSessionID;
        /// <summary>Agent first name</summary>
        public string FirstName;
        /// <summary>Agent last name</summary>
        public string LastName;
        /// <summary>Short string identifying the reason for login failure, such as 'key'
        /// for bad username or password</summary>
        public string ErrorReason;
        /// <summary>Welcome message, or longer description of the error reason</summary>
        public string Message;
        /// <summary>Starting simulator location. Either last, home, or a simulator name and position</summary>
        public string StartLocation;
        /// <summary>UDP port of the simulator being logged into</summary>
        public uint SimPort;
        /// <summary>IPv4 address of the simulator being logged into</summary>
        public string SimAddress;
        /// <summary>Circuit code to communicate with the simulator, used in the Linden UDP protocol</summary>
        public int CircuitCode;
        /// <summary>Grid X coordinate of the simulator being logged into</summary>
        public uint RegionX;
        /// <summary>Grid Y coordinate of the simulator being logged into</summary>
        public uint RegionY;
        /// <summary>Direction the avatar is looking upon login</summary>
        public Vector3 LookAt;
        /// <summary>Grid X coordinate of the home simulator</summary>
        public uint HomeRegionX;
        /// <summary>Grid Y coordinate of the home simulator</summary>
        public uint HomeRegionY;
        /// <summary>Home position</summary>
        public Vector3 HomePosition;
        /// <summary>Direction the avatar is looking when teleporting home</summary>
        public Vector3 HomeLookAt;
        /// <summary>Gender and inventory folder containing the avatar outfit to wear</summary>
        public Hashtable InitialOutfit;
        /// <summary>Root folder in the agent's inventory</summary>
        public UUID InventoryRoot;
        /// <summary>Folder list of the agent's inventory</summary>
        public ArrayList AgentInventory;
        /// <summary>Root folder in the library inventory</summary>
        public UUID InventoryLibRoot;
        /// <summary>Folder list of the library inventory</summary>
        public ArrayList InventoryLibrary;
        /// <summary>List of active gesture inventory items</summary>
        public ArrayList ActiveGestures;
        /// <summary>Buddy list for this avatar</summary>
        public Hashtable BuddyList;
        /// <summary>Seed capability URL to request further capabilities from</summary>
        public string SeedCapability;

        #endregion Active parameters

        #region Automatic or unused parameters

        /// <summary>Daylight savings time</summary>
        public string DST = (TimeZone.CurrentTimeZone.IsDaylightSavingTime(DateTime.UtcNow) ? "Y" : "N");
        /// <summary>Owner of the library inventory</summary>
        public UUID InventoryLibraryOwner = UUID.Zero;
        /// <summary>Unused</summary>
        public ArrayList EventCategories = new ArrayList();
        /// <summary>Unused</summary>
        public string StipendSinceLogin = "N";
        /// <summary>Unused</summary>
        public string Gendered = "Y";
        /// <summary>Unused</summary>
        public string EverLoggedIn = "Y";
        /// <summary>Unused</summary>
        public string AgentAccess = "M";
        /// <summary>Flag to tell the client whether to enable profile webpages or not</summary>
        public string AllowFirstLife = "Y";
        /// <summary>Sun texture</summary>
        public UUID SunTexture = new UUID("cce0f112-878f-4586-a2e2-a8f104bba271");
        /// <summary>Cloud texture</summary>
        public UUID CloudTexture = new UUID("dc4b9f0b-d008-45c6-96a4-01dd947ac621");
        /// <summary>Moon texture</summary>
        public UUID MoonTexture = new UUID("d07f6eed-b96a-47cd-b51d-400ad4a1c428");
        /// <summary>Categories for classified ads</summary>
        public ArrayList ClassifiedCategories = new ArrayList();

        #endregion Automatic or unused parameters

        public LindenLoginData()
        {
        }

        public void AddClassifiedCategory(int id, string categoryName)
        {
            Hashtable hash = new Hashtable(2);
            hash["category_name"] = categoryName;
            hash["category_id"] = id;
            ClassifiedCategories.Add(hash);
        }

        public void SetInitialOutfit(string folderName, bool male)
        {
            if (InitialOutfit == null)
                InitialOutfit = new Hashtable(2);

            InitialOutfit["folder_name"] = folderName;
            InitialOutfit["gender"] = (male ? "male" : "female");
        }

        public XmlRpcResponse ToXmlRpcResponse()
        {
            try
            {
                Hashtable responseData = new Hashtable();

                // Login flags
                Hashtable loginFlagsHash = new Hashtable(4);
                loginFlagsHash["daylight_savings"] = DST;
                loginFlagsHash["stipend_since_login"] = StipendSinceLogin;
                loginFlagsHash["gendered"] = Gendered;
                loginFlagsHash["ever_logged_in"] = EverLoggedIn;
                ArrayList loginFlags = new ArrayList(1);
                loginFlags.Add(loginFlagsHash);
                responseData["login-flags"] = loginFlags;

                // Global textures
                Hashtable globalTexturesHash = new Hashtable(3);
                globalTexturesHash["sun_texture_id"] = SunTexture.ToString();
                globalTexturesHash["cloud_texture_id"] = CloudTexture.ToString();
                globalTexturesHash["moon_texture_id"] = MoonTexture.ToString();
                ArrayList globalTextures = new ArrayList(1);
                globalTextures.Add(globalTexturesHash);
                responseData["global-textures"] = globalTextures;

                // Event categories are unused
                ArrayList eventCategories = new ArrayList(1);
                Hashtable category = new Hashtable(2);
                category["category_name"] = "Defalt Event Category";
                category["category_id"] = 20;
                eventCategories.Add(category);
                responseData["event_categories"] = eventCategories;

                // Event notifications are unused
                responseData["event_notifications"] = new ArrayList(0);

                // UI Config
                Hashtable uiConfigHash = new Hashtable(1);
                uiConfigHash["allow_first_life"] = AllowFirstLife;
                ArrayList uiConfig = new ArrayList(1);
                uiConfig.Add(uiConfigHash);
                responseData["ui-config"] = uiConfig;

                // {'region_handle':[r{0},r{1}], 'position':[r{2},r{3},r{4}], 'look_at':[r{5},r{6},r{7}]}
                System.Text.StringBuilder homeString = new System.Text.StringBuilder("{'region_handle':[r");
                homeString.Append(256 * HomeRegionX);
                homeString.Append(",r");
                homeString.Append(256 * HomeRegionY);
                homeString.Append("], 'position':[r");
                homeString.Append(HomePosition.X);
                homeString.Append(",r");
                homeString.Append(HomePosition.Y);
                homeString.Append(",r");
                homeString.Append(HomePosition.Z);
                homeString.Append("], 'look_at':[r");
                homeString.Append(HomeLookAt.X);
                homeString.Append(",r");
                homeString.Append(HomeLookAt.Y);
                homeString.Append(",r");
                homeString.Append(HomeLookAt.Z);
                homeString.Append("]}");
                responseData["home"] = homeString.ToString();

                // LookAt
                responseData["look_at"] = String.Format("[r{0},r{1},r{2}]", LookAt.X, LookAt.Y, LookAt.Z);

                // Inventory root
                Hashtable inventoryRootHash = new Hashtable(1);
                inventoryRootHash["folder_id"] = InventoryRoot.ToString();
                ArrayList inventoryRoot = new ArrayList(1);
                inventoryRoot.Add(inventoryRootHash);
                responseData["inventory-root"] = inventoryRoot;

                // Inventory library root
                Hashtable inventoryLibRootHash = new Hashtable(1);
                inventoryLibRootHash["folder_id"] = InventoryLibRoot.ToString();
                ArrayList inventoryLibRoot = new ArrayList(1);
                inventoryLibRoot.Add(inventoryLibRootHash);
                responseData["inventory-lib-root"] = inventoryLibRoot;

                // Inventory library owner
                Hashtable inventoryLibOwnerHash = new Hashtable(1);
                inventoryLibOwnerHash["agent_id"] = InventoryLibraryOwner.ToString();
                ArrayList inventoryLibOwner = new ArrayList(1);
                inventoryLibOwner.Add(inventoryLibOwnerHash);
                responseData["inventory-lib-owner"] = inventoryLibOwner;

                // Inventory skeletons
                responseData["inventory-skeleton"] = AgentInventory;
                responseData["inventory-skel-lib"] = InventoryLibrary;

                //
                responseData["login"] = Login.ToString().ToLower();
                responseData["first_name"] = FirstName;
                responseData["last_name"] = LastName;
                responseData["agent_access"] = AgentAccess;
                responseData["sim_port"] = (int)SimPort;
                responseData["sim_ip"] = SimAddress;
                responseData["agent_id"] = AgentID.ToString();
                responseData["session_id"] = SessionID.ToString();
                responseData["secure_session_id"] = SecureSessionID.ToString();
                responseData["circuit_code"] = CircuitCode;
                responseData["seconds_since_epoch"] = (int)OpenMetaverse.Utils.DateTimeToUnixTime(DateTime.UtcNow);
                responseData["seed_capability"] = SeedCapability;
                responseData["classified_categories"] = ClassifiedCategories;
                responseData["gestures"] = ActiveGestures;

                if (InitialOutfit != null)
                {
                    ArrayList initialOutfitArray = new ArrayList(1);
                    initialOutfitArray.Add(InitialOutfit);
                    responseData["initial-outfit"] = initialOutfitArray;
                }
                else
                    responseData["initial-outfit"] = new ArrayList(0);

                responseData["start_location"] = StartLocation;
                responseData["message"] = Message;
                responseData["reason"] = String.Empty;

                responseData["region_x"] = (int)(RegionX * 256);
                responseData["region_y"] = (int)(RegionY * 256);

                if (BuddyList != null)
                {
                    ArrayList buddyList = new ArrayList();
                    buddyList.Add(BuddyList);
                    responseData["buddy-list"] = buddyList;
                }

                XmlRpcResponse response = new XmlRpcResponse();
                response.Value = responseData;
                return response;
            }
            catch (Exception ex)
            {
                m_log.Error("Error creating XML-RPC login response: " + ex.Message, ex);
                return GenerateFailureResponse("Internal Error", "Error generating login response", false);
            }
        }

        public XmlRpcResponse GenerateFailureResponse(string reason, string message, bool loginSuccess)
        {
            Hashtable loginError = new Hashtable(3);
            loginError["reason"] = reason;
            loginError["message"] = message;
            loginError["login"] = loginSuccess.ToString().ToLower();

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = loginError;
            return response;
        }
    }
}
