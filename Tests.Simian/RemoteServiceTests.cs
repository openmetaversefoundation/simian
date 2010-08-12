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
using System.Net;
using System.Collections.Generic;
using System.Text;
using Simian;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using NUnit.Framework;
using Simian.Connectors.Remote;

using InventoryBase = Simian.InventoryBase;
using InventoryItem = Simian.InventoryItem;
using InventoryFolder = Simian.InventoryFolder;

namespace Tests.Simian
{

    [TestFixture]
    public class RemoteAssetServiceTests
    {
        private UUID m_TestAssetID = UUID.Random();
        private ServicesClient m_Client;
        
        [TestFixtureSetUp]
        public void SetupAssetTests()
        {
            m_Client = new ServicesClient(new Uri("http://thorium.npl.com/simian/src/simian/trunk/Grid/Services/assets/"));

            UUID tmp;
            byte[] data = System.IO.File.ReadAllBytes(@".\DefaultAssets\Plywood-89556747-24cb-43ed-920b-47caed15465f.j2c");
            Assert.IsTrue(m_Client.TryStoreRemoteAsset(m_TestAssetID, "image/x-j2c", data, UUID.Random(), out tmp));
            Assert.AreEqual(m_TestAssetID, tmp);
            Console.WriteLine(tmp.ToString());

        }

        [TestFixtureTearDown]
        public void CleanupAssetTests()
        {
            Assert.IsTrue(m_Client.TryRemoveRemoteAsset(m_TestAssetID, "image/x-j2c"));
        }

        [Test]
        [Category("Asset")]
        public void GetAssetTest()
        {
            Asset tmp = new Asset();
            Assert.IsTrue(m_Client.TryGetAsset(m_TestAssetID, "image/x-j2c", out tmp));
            Assert.AreEqual(tmp.ID, m_TestAssetID);
        }
    }

    [TestFixture]
    public class RemoteUserServiceTests
    {
        private ServicesClient m_Client;
        private UUID m_SceneID = UUID.Random();
        private UUID m_UserID;
        private UUID m_InventoryRootID;


        [TestFixtureSetUp]
        public void CreateClient()
        {
            m_Client = new ServicesClient(new Uri("http://thorium.npl.com/simian/src/simian/trunk/Grid/Services/services/"));
            Assert.IsTrue(m_Client.TryAddScene(m_SceneID, "Test Scene Freedom", new Vector3d(256,256,768), new Vector3d(512, 512, 1024), new Uri("http://127.0.0.1:8121")), "Error Adding Scene");
            //Assert.IsTrue(m_Client.TryAddScene(UUID.Random(), "1", new Vector3d(0, 0, 0), new Vector3d(256, 256, 256), new Uri("http://127.0.0.1:8122")), "Error Adding Scene");
            //Assert.IsTrue(m_Client.TryAddScene(UUID.Random(), "2", new Vector3d(256, 0, 0), new Vector3d(512, 256, 256), new Uri("http://127.0.0.1:8122")), "Error Adding Scene");
            //Assert.IsTrue(m_Client.TryAddScene(UUID.Random(), "3", new Vector3d(0, 256, 0), new Vector3d(256, 512, 256), new Uri("http://127.0.0.1:8122")), "Error Adding Scene");
            //Assert.IsTrue(m_Client.TryAddScene(UUID.Random(), "4", new Vector3d(256, 256, 0), new Vector3d(512, 512, 256), new Uri("http://127.0.0.1:8122")), "Error Adding Scene");
            //Assert.IsTrue(m_Client.TryAddScene(UUID.Random(), "5", new Vector3d(256, 512, 0), new Vector3d(512, 768, 256), new Uri("http://127.0.0.1:8122")), "Error Adding Scene");
            //Assert.IsTrue(m_Client.TryAddScene(UUID.Random(), "6", new Vector3d(0, 512, 256), new Vector3d(256, 768, 512), new Uri("http://127.0.0.1:8122")), "Error Adding Scene");
            //Assert.IsTrue(m_Client.TryAddScene(UUID.Random(), "7", new Vector3d(0, 768, 512), new Vector3d(256, 1024, 768), new Uri("http://127.0.0.1:8122")), "Error Adding Scene");
            //Assert.IsTrue(m_Client.TryAddScene(UUID.Random(), "8", new Vector3d(256, 1024, 512), new Vector3d(512, 1280, 768), new Uri("http://127.0.0.1:8122")), "Error Adding Scene");
            
            User tmp;
            Assert.IsTrue(m_Client.TryAddUser("Test User1", m_SceneID, Vector3d.Zero, Vector3.Zero, new OSDMap(), out tmp), "Error Adding User");
            Assert.AreEqual(m_SceneID, tmp.HomeLocation);
            m_UserID = tmp.ID;
                        
            Assert.IsTrue(m_Client.TryAddIdentity(m_UserID, "Test Identity1", Utils.MD5("Test Password"), String.Empty), "Error Adding Identity");
            Assert.IsTrue(m_Client.TryAddPresence(m_UserID, m_SceneID, Vector3d.Zero, Vector3.Zero), "Error Adding User Presence");            
            Assert.IsTrue(m_Client.TryCreateInventorySkeleton(m_UserID, "Unit Test Root", out m_InventoryRootID), "Error Creating Inventory Skeleton");
        }

        [TestFixtureTearDown]
        public void CleanupTests()
        {            
            Assert.IsTrue(m_Client.TryRemoveScene(m_SceneID), "Error Removing Scene");
            Assert.IsTrue(m_Client.TryRemoveIdentity("Test Identity1"), "Error Removing Identity");
            Assert.IsTrue(m_Client.TryRemoveUser(m_UserID), "Error Removing User");
            Assert.IsTrue(m_Client.TryRemovePresence(m_UserID), "Error removing presence");
            Assert.IsTrue(m_Client.TryRemoveInventoryFolder(m_InventoryRootID, true), "Error Removing Inventory Skeleton");            
        }

        [Test]
        [Category("Scene")]
        public void GetSceneByNameTest()
        {
            string tmp;
            Assert.IsTrue(m_Client.TryGetSceneByName("Test Scene Freedom", out tmp));
        }

        [Test]
        [Category("Scene")]
        public void GetSceneByIDTest()
        {
            string tmp;
            Assert.IsTrue(m_Client.TryGetSceneByID(m_SceneID, out tmp));
        }

        [Test]
        [Category("Scene")]
        public void GetSceneNeighborsTest()
        {
            List<SceneInfo> found = null;
            bool b = m_Client.GetSceneNeighbors(m_SceneID, out found);
            Assert.IsTrue(b);
            Assert.IsNotNull(found);
            foreach (SceneInfo scene in found)
            {
                Console.WriteLine(scene.ID + " " + scene.Name);
            }
        }

        [Test]
        [Category("Scene")]
        public void GetSceneInVectorTest()
        {
            SceneInfo tmp;
            Assert.IsTrue(m_Client.TryGetSceneInVector(new Vector3d(384, 384, 0), out tmp));
            Assert.AreEqual("Test Scene Freedom", tmp.Name);
            Assert.AreNotEqual(UUID.Zero, tmp.ID);
        }

        [Test]
        [Category("Scene")]
        public void GetSceneNearVectorTest()
        {
            SceneInfo tmp;
            Assert.IsTrue(m_Client.TryGetSceneNearVector(new Vector3d(128, 128, 25), out tmp));
        }

        [Test]
        [Category("Scene")]
        public void SearchScenesTest()
        {
            SceneInfo[] found = m_Client.SearchScenes("Test");
            Assert.Greater(found.Length, 0);
        }

        [Test]
        [Category("User")]
        public void GetUserTest()
        {
            User user;
            Assert.IsTrue(m_Client.TryGetUser(m_UserID, out user));
            Assert.AreEqual(user.ID, m_UserID);
        }
        
        [Test]
        [Category("Identity")]
        public void AuthorizeIdentityTest()
        {
            UUID userID;
            Assert.IsTrue(m_Client.TryAuthorizeIdentity("Test Identity1", Utils.MD5("Test Password"), String.Empty, out userID));
            Assert.AreNotEqual(userID, UUID.Zero);
        }

        [Test]
        [Category("Identity")]
        public void GetIdentitiesTest()
        {
            Identity[] identities;
            Assert.IsTrue(m_Client.TryGetIdentities(m_UserID, out identities));
            Assert.GreaterOrEqual(identities.Length, 0);
        }

        [Test]
        [Category("Presence")]
        public void UpdatePresenceTest()
        {            
            Assert.IsTrue(m_Client.TryUpdatePresence(m_UserID, m_SceneID, new Vector3d(128, 128, 128), new Vector3d(128, 128, -1)));
        }
        [Test]
        [Category("Presence")]
        public void GetPresenceTest()
        {
            string tmp;
            Assert.IsTrue(m_Client.TryGetPresence(m_UserID, out tmp));
        }

        [Test]
        [Category("User")]
        public void FriendMapTest()
        {
            string tmp;
            Assert.IsTrue(m_Client.TryGetFriendMap(m_UserID, out tmp));
        }

        [Test]
        [Category("User")]
        public void SearchUsersTest()
        {
            User[] users = m_Client.SearchUsers("Test");
            Assert.Greater(users.Length, 0);
        }

        [Test]
        [Category("User")]
        public void UpdateUserTest()
        {
            User found;
            Assert.IsTrue(m_Client.TryGetUser(m_UserID, out found));
            
            Assert.IsTrue(m_Client.TryUpdateUser(found.ID, "Test Update", found.HomeLocation, found.HomeLookAt, found.HomePosition));
            
            User found2 = found;            
            Assert.IsTrue(m_Client.TryGetUser(m_UserID, out found2));            
            Assert.AreNotSame(found.Name, found2.Name);
            Assert.AreEqual(found2.Name, "Test Update");
        }

        [Test]
        [Category("Inventory")]
        public void GetRootFolderTest()
        {
            UUID folder;
            Assert.IsTrue(m_Client.TryGetRootFolder(m_UserID, out folder));
            Assert.AreEqual(folder, m_InventoryRootID);
        }
        
        [Test]
        [Category("Inventory")]
        public void GetInventoryLibRootTest()
        {
            UUID libRootID;
            UUID libOwnerID;
            Assert.IsTrue(m_Client.TryGetLibraryInfo(out libRootID, out libOwnerID));
            Assert.AreNotEqual(libOwnerID, UUID.Zero);
            Assert.AreNotEqual(libRootID, UUID.Zero);
        }

        [Test]
        [Category("Inventory")]
        public void CreateRemoveInventoryFolderTest()
        {
            UUID folderID;
            Assert.IsTrue(m_Client.TryAddInventoryFolder(m_InventoryRootID, m_UserID, "Unit Test Folder", String.Empty, out folderID));            
            Assert.IsTrue(m_Client.TryRemoveInventoryFolder(folderID, false));
        }

        [Test]
        [Category("Inventory")]
        public void GetAssetIdsTest()
        {
            UUID[] req = new UUID[3] { UUID.Random(), UUID.Random(), UUID.Random() };
            Dictionary<UUID, UUID> assetIds;
            Assert.IsTrue(m_Client.TryGetInventoryAssetList(m_UserID, req, out assetIds));
        }

        [Test]
        [Category("Inventory")]
        public void CreateGetRemoveInventoryItemTest()
        {
            UUID folderID;
            Assert.IsTrue(m_Client.TryAddInventoryFolder(m_InventoryRootID, m_UserID, "Unit Test Folder2", String.Empty, out folderID));
            InventoryItem tmpII = new InventoryItem()
            {
                AssetID = UUID.Random(),
                ContentType = "application/octet-stream",
                CreationDate = DateTime.UtcNow,
                CreatorID = m_UserID,
                Description = "Unit Test Description",
                ExtraData = new OSDMap(),
                Name = "Unit Test Item",
                OwnerID = m_UserID,
                ParentID = folderID
            };
            
            Assert.IsTrue(m_Client.TryAddInventoryItem(tmpII, out tmpII.ID));
            Assert.AreNotEqual(tmpII.ID, UUID.Zero);

            InventoryBase inv;
            Assert.IsTrue(m_Client.TryGetInventory(m_UserID, tmpII.ID, true, true, true, out inv));
            InventoryItem foundItem = (InventoryItem)inv;
            Assert.AreEqual(foundItem.ID, tmpII.ID);
            Assert.AreEqual(foundItem.ParentID, folderID);
            Assert.AreEqual(foundItem.Name, tmpII.Name);
            Assert.AreEqual(foundItem.Description, tmpII.Description);
            Assert.AreEqual(foundItem.OwnerID, tmpII.OwnerID);
            Assert.AreEqual(foundItem.AssetID, tmpII.AssetID);
            // TODO: gotta look into a possible bug when deserializing UTC Unix Timestamps
            //Assert.AreEqual(foundItem.CreationDate, tmpII.CreationDate);
            Assert.AreEqual(foundItem.CreatorID, tmpII.CreatorID);
            //Assert.AreEqual(foundItem.ExtraData, tmpII.ExtraData);

            Assert.IsTrue(m_Client.TryRemoveInventoryItem(tmpII.ID));
            Assert.IsTrue(m_Client.TryRemoveInventoryFolder(folderID, false));
        }

    }
}
