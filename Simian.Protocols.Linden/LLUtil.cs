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
using HttpServer;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    public static class LLUtil
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        #region HTTP / OSD

        public static bool TryGetOSDMap(System.IO.Stream stream, out OSDMap map)
        {
            try
            {
                // DEBUG:
                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, (int)stream.Length);
                m_log.Debug(System.Text.Encoding.UTF8.GetString(buffer));
                map = OSDParser.Deserialize(buffer) as OSDMap;

                //map = OSDParser.Deserialize(request.Body) as OSDMap;
            }
            catch (Exception ex)
            {
                m_log.Warn("Failed to deserialize incoming data: " + ex.Message);
                map = null;
            }

            return (map != null);
        }

        public static bool TryGetMessage<TMessage>(System.IO.Stream stream, out TMessage message)
            where TMessage : class, IMessage, new()
        {
            message = null;

            try
            {
                // DEBUG:
                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, (int)stream.Length);
                m_log.Debug(System.Text.Encoding.UTF8.GetString(buffer));
                OSDMap map = OSDParser.Deserialize(buffer) as OSDMap;

                //OSDMap map = OSDParser.Deserialize(request.Body) as OSDMap;

                if (map != null)
                {
                    message = new TMessage();
                    message.Deserialize(map);
                }
            }
            catch (Exception ex)
            {
                m_log.Warn("Failed to deserialize incoming data: " + ex.Message);
            }

            return (message != null);
        }

        public static void SendLLSDXMLResponse(IHttpResponse response, OSD osd)
        {
            if (osd != null)
            {
                try
                {
                    byte[] data = OSDParser.SerializeLLSDXmlBytes(osd);

                    response.ContentLength = data.Length;
                    response.Body.Write(data, 0, data.Length);
                }
                catch (Exception ex)
                {
                    m_log.Warn("Failed to send response data: " + ex.Message);
                }
            }
        }

        #endregion HTTP / OSD

        #region LL / file extension / content-type conversions

        public static string LLAssetTypeToContentType(int assetType)
        {
            switch ((AssetType)assetType)
            {
                case AssetType.Texture:
                    return "image/x-j2c";
                case AssetType.Sound:
                    return "audio/ogg";
                case AssetType.CallingCard:
                    return "application/vnd.ll.callingcard";
                case AssetType.Landmark:
                    return "application/vnd.ll.landmark";
                case AssetType.Clothing:
                    return "application/vnd.ll.clothing";
                case AssetType.Object:
                    return "application/vnd.ll.primitive";
                case AssetType.Notecard:
                    return "application/vnd.ll.notecard";
                case AssetType.Folder:
                    return "application/vnd.ll.folder";
                case AssetType.RootFolder:
                    return "application/vnd.ll.rootfolder";
                case AssetType.LSLText:
                    return "application/vnd.ll.lsltext";
                case AssetType.LSLBytecode:
                    return "application/vnd.ll.lslbyte";
                case AssetType.TextureTGA:
                case AssetType.ImageTGA:
                    return "image/tga";
                case AssetType.Bodypart:
                    return "application/vnd.ll.bodypart";
                case AssetType.TrashFolder:
                    return "application/vnd.ll.trashfolder";
                case AssetType.SnapshotFolder:
                    return "application/vnd.ll.snapshotfolder";
                case AssetType.LostAndFoundFolder:
                    return "application/vnd.ll.lostandfoundfolder";
                case AssetType.SoundWAV:
                    return "audio/x-wav";
                case AssetType.ImageJPEG:
                    return "image/jpeg";
                case AssetType.Animation:
                    return "application/vnd.ll.animation";
                case AssetType.Gesture:
                    return "application/vnd.ll.gesture";
                case AssetType.Simstate:
                    return "application/x-metaverse-simstate";
                case AssetType.FavoriteFolder:
                    return "application/vnd.ll.favoritefolder";
                case AssetType.Link:
                    return "application/vnd.ll.link";
                case AssetType.LinkFolder:
                    return "application/vnd.ll.linkfolder";
                case AssetType.CurrentOutfitFolder:
                    return "application/vnd.ll.currentoutfitfolder";
                case AssetType.OutfitFolder:
                    return "application/vnd.ll.outfitfolder";
                case AssetType.MyOutfitsFolder:
                    return "application/vnd.ll.myoutfitsfolder";
                case AssetType.Mesh:
                    return "application/vnd.ll.mesh";
                case AssetType.Unknown:
                default:
                    return "application/octet-stream";
            }
        }

        public static string LLInvTypeToContentType(int invType)
        {
            switch ((InventoryType)invType)
            {
                case InventoryType.Animation:
                    return "application/vnd.ll.animation";
                case InventoryType.CallingCard:
                    return "application/vnd.ll.callingcard";
                case InventoryType.Folder:
                    return "application/vnd.ll.folder";
                case InventoryType.Gesture:
                    return "application/vnd.ll.gesture";
                case InventoryType.Landmark:
                    return "application/vnd.ll.landmark";
                case InventoryType.LSL:
                    return "application/vnd.ll.lsltext";
                case InventoryType.Notecard:
                    return "application/vnd.ll.notecard";
                case InventoryType.Attachment:
                case InventoryType.Object:
                    return "application/vnd.ll.primitive";
                case InventoryType.Sound:
                    return "audio/ogg";
                case InventoryType.Snapshot:
                case InventoryType.Texture:
                    return "image/x-j2c";
                case InventoryType.Wearable:
                    return "application/vnd.ll.clothing";
                case InventoryType.Mesh:
                    return "application/vnd.ll.mesh";
                default:
                    return "application/octet-stream";
            }
        }

        public static sbyte ContentTypeToLLAssetType(string contentType)
        {
            switch (contentType)
            {
                case "image/x-j2c":
                case "image/jp2":
                    return (sbyte)AssetType.Texture;
                case "application/ogg":
                case "audio/ogg":
                    return (sbyte)AssetType.Sound;
                case "application/vnd.ll.callingcard":
                case "application/x-metaverse-callingcard":
                    return (sbyte)AssetType.CallingCard;
                case "application/vnd.ll.landmark":
                case "application/x-metaverse-landmark":
                    return (sbyte)AssetType.Landmark;
                case "application/vnd.ll.clothing":
                case "application/x-metaverse-clothing":
                    return (sbyte)AssetType.Clothing;
                case "application/vnd.ll.primitive":
                case "application/x-metaverse-primitive":
                    return (sbyte)AssetType.Object;
                case "application/vnd.ll.notecard":
                case "application/x-metaverse-notecard":
                    return (sbyte)AssetType.Notecard;
                case "application/vnd.ll.folder":
                    return (sbyte)AssetType.Folder;
                case "application/vnd.ll.rootfolder":
                    return (sbyte)AssetType.RootFolder;
                case "application/vnd.ll.lsltext":
                case "application/x-metaverse-lsl":
                    return (sbyte)AssetType.LSLText;
                case "application/vnd.ll.lslbyte":
                case "application/x-metaverse-lso":
                    return (sbyte)AssetType.LSLBytecode;
                case "image/tga":
                    // Note that AssetType.TextureTGA will be converted to AssetType.ImageTGA
                    return (sbyte)AssetType.ImageTGA;
                case "application/vnd.ll.bodypart":
                case "application/x-metaverse-bodypart":
                    return (sbyte)AssetType.Bodypart;
                case "application/vnd.ll.trashfolder":
                    return (sbyte)AssetType.TrashFolder;
                case "application/vnd.ll.snapshotfolder":
                    return (sbyte)AssetType.SnapshotFolder;
                case "application/vnd.ll.lostandfoundfolder":
                    return (sbyte)AssetType.LostAndFoundFolder;
                case "audio/x-wav":
                    return (sbyte)AssetType.SoundWAV;
                case "image/jpeg":
                    return (sbyte)AssetType.ImageJPEG;
                case "application/vnd.ll.animation":
                case "application/x-metaverse-animation":
                    return (sbyte)AssetType.Animation;
                case "application/vnd.ll.gesture":
                case "application/x-metaverse-gesture":
                    return (sbyte)AssetType.Gesture;
                case "application/x-metaverse-simstate":
                    return (sbyte)AssetType.Simstate;
                case "application/vnd.ll.favoritefolder":
                    return (sbyte)AssetType.FavoriteFolder;
                case "application/vnd.ll.link":
                    return (sbyte)AssetType.Link;
                case "application/vnd.ll.linkfolder":
                    return (sbyte)AssetType.LinkFolder;
                case "application/vnd.ll.currentoutfitfolder":
                    return (sbyte)AssetType.CurrentOutfitFolder;
                case "application/vnd.ll.outfitfolder":
                    return (sbyte)AssetType.OutfitFolder;
                case "application/vnd.ll.myoutfitsfolder":
                    return (sbyte)AssetType.MyOutfitsFolder;
                case "application/vnd.ll.mesh":
                    return (sbyte)AssetType.Mesh;
                case "application/octet-stream":
                default:
                    return (sbyte)AssetType.Unknown;
            }
        }

        public static sbyte ContentTypeToLLInvType(string contentType)
        {
            switch (contentType)
            {
                case "image/x-j2c":
                case "image/jp2":
                case "image/tga":
                case "image/jpeg":
                    return (sbyte)InventoryType.Texture;
                case "application/ogg":
                case "audio/ogg":
                case "audio/x-wav":
                    return (sbyte)InventoryType.Sound;
                case "application/vnd.ll.callingcard":
                case "application/x-metaverse-callingcard":
                    return (sbyte)InventoryType.CallingCard;
                case "application/vnd.ll.landmark":
                case "application/x-metaverse-landmark":
                    return (sbyte)InventoryType.Landmark;
                case "application/vnd.ll.clothing":
                case "application/x-metaverse-clothing":
                case "application/vnd.ll.bodypart":
                case "application/x-metaverse-bodypart":
                    return (sbyte)InventoryType.Wearable;
                case "application/vnd.ll.primitive":
                case "application/x-metaverse-primitive":
                    return (sbyte)InventoryType.Object;
                case "application/vnd.ll.notecard":
                case "application/x-metaverse-notecard":
                    return (sbyte)InventoryType.Notecard;
                case "application/vnd.ll.folder":
                    return (sbyte)InventoryType.Folder;
                case "application/vnd.ll.rootfolder":
                    return (sbyte)InventoryType.RootCategory;
                case "application/vnd.ll.lsltext":
                case "application/x-metaverse-lsl":
                case "application/vnd.ll.lslbyte":
                case "application/x-metaverse-lso":
                    return (sbyte)InventoryType.LSL;
                case "application/vnd.ll.trashfolder":
                case "application/vnd.ll.snapshotfolder":
                case "application/vnd.ll.lostandfoundfolder":
                    return (sbyte)InventoryType.Folder;
                case "application/vnd.ll.animation":
                case "application/x-metaverse-animation":
                    return (sbyte)InventoryType.Animation;
                case "application/vnd.ll.gesture":
                case "application/x-metaverse-gesture":
                    return (sbyte)InventoryType.Gesture;
                case "application/x-metaverse-simstate":
                    return (sbyte)InventoryType.Snapshot;
                case "application/vnd.ll.mesh":
                    return (sbyte)InventoryType.Mesh;
                case "application/octet-stream":
                default:
                    return (sbyte)InventoryType.Unknown;
            }
        }

        #endregion SL / file extension / content-type conversions

        #region Appearance Serialization

        public static OSDMap SerializeAppearance(IAssetClient assetClient, Primitive.TextureEntry texture, byte[] visualParams)
        {
            // Fetch all of the texture assets
            Dictionary<UUID, byte[]> textures = new Dictionary<UUID, byte[]>();
            for (int i = 0; i < texture.FaceTextures.Length; i++)
            {
                Primitive.TextureEntryFace face = texture.FaceTextures[i];
                if (face != null && face.TextureID != AppearanceManager.DEFAULT_AVATAR_TEXTURE && !textures.ContainsKey(face.TextureID))
                {
                    // Fetch this texture
                    Asset asset;
                    if (assetClient.TryGetAsset(face.TextureID, "image/x-j2c", out asset))
                        textures.Add(face.TextureID, asset.Data);
                }
            }

            OSDMap assets = new OSDMap(textures.Count);
            foreach (KeyValuePair<UUID, byte[]> kvp in textures)
                assets.Add(kvp.Key.ToString(), OSD.FromBinary(kvp.Value));

            return new OSDMap
            {
                { "texture", texture.GetOSD() },
                { "parameters", OSD.FromBinary(visualParams) },
                { "assets", assets }
            };
        }

        public static bool DeserializeAppearance(OSDMap appearance, out Primitive.TextureEntry texture, out byte[] visualParams, out Dictionary<UUID, byte[]> assets)
        {
            if (appearance.ContainsKey("texture") && appearance.ContainsKey("parameters") && appearance["assets"].Type == OSDType.Map)
            {
                texture = Primitive.TextureEntry.FromOSD(appearance["texture"]);
                visualParams = appearance["parameters"].AsBinary();

                OSDMap assetMap = (OSDMap)appearance["assets"];
                assets = new Dictionary<UUID,byte[]>(assetMap.Count);
                foreach (KeyValuePair<string, OSD> kvp in assetMap)
                {
                    UUID assetID;
                    if (UUID.TryParse(kvp.Key, out assetID))
                        assets[assetID] = kvp.Value.AsBinary();
                }

                return texture != null && visualParams.Length > 1;
            }
            else
            {
                texture = null;
                visualParams = null;
                assets = null;
                return false;
            }
        }

        #endregion Appearance Serialization

        public static ObjectPropertiesPacket.ObjectDataBlock BuildEntityPropertiesBlock(ISceneEntity entity)
        {
            OpenMetaverse.Packets.ObjectPropertiesPacket.ObjectDataBlock block = new OpenMetaverse.Packets.ObjectPropertiesPacket.ObjectDataBlock();
            block.GroupID = entity.GroupID;
            block.Name = Utils.StringToBytes(entity.Name);
            block.ObjectID = entity.ID;
            block.OwnerID = entity.OwnerID;

            if (entity is LLPrimitive)
            {
                Primitive prim = ((LLPrimitive)entity).Prim;

                block.AggregatePerms = prim.Properties.AggregatePerms;
                block.AggregatePermTextures = prim.Properties.AggregatePermTextures;
                block.AggregatePermTexturesOwner = prim.Properties.AggregatePermTexturesOwner;
                block.BaseMask = (uint)prim.Properties.Permissions.BaseMask;
                block.Category = (uint)prim.Properties.Category;
                block.CreationDate = Utils.DateTimeToUnixTime(prim.Properties.CreationDate);
                block.CreatorID = prim.Properties.CreatorID;
                block.Description = Utils.StringToBytes(prim.Properties.Description);
                block.EveryoneMask = (uint)prim.Properties.Permissions.EveryoneMask;
                block.FolderID = prim.Properties.FolderID;
                block.FromTaskID = prim.Properties.FromTaskID;
                block.GroupMask = (uint)prim.Properties.Permissions.GroupMask;
                block.InventorySerial = prim.Properties.InventorySerial;
                block.ItemID = prim.Properties.ItemID;
                block.LastOwnerID = prim.Properties.LastOwnerID;
                block.NextOwnerMask = (uint)prim.Properties.Permissions.NextOwnerMask;
                block.OwnerMask = (uint)prim.Properties.Permissions.OwnerMask;
                block.OwnershipCost = prim.Properties.OwnershipCost;
                block.SalePrice = prim.Properties.SalePrice;
                block.SaleType = (byte)prim.Properties.SaleType;
                block.SitName = Utils.StringToBytes(prim.Properties.SitName);
                block.TextureID = prim.Properties.GetTextureIDBytes();
                block.TouchName = Utils.StringToBytes(prim.Properties.TouchName);
            }
            else
            {
                block.Description = Utils.EmptyBytes;
                block.SitName = Utils.EmptyBytes;
                block.TextureID = Utils.EmptyBytes;
                block.TouchName = Utils.EmptyBytes;
            }

            return block;
        }

        /// <summary>
        /// Translates an llSitTarget into the actual link offset based on the scale of the avatar sitting down
        /// </summary>
        /// <param name="sitTarget"></param>
        /// <param name="agentScale"></param>
        /// <returns></returns>
        public static Vector3 GetSitTarget(Vector3 sitTarget, Vector3 agentScale)
        {
            return sitTarget + new Vector3(0f, 0f, 0.4f - (agentScale.Z * 0.02638f));
        }

        #region PrimObject Conversion

        public static LLPrimitive PrimObjectToLLPrim(PrimObject obj, IScene scene, IPrimMesher mesher)
        {
            Primitive prim = new Primitive();
            prim.Properties = new Primitive.ObjectProperties();

            LLPrimitive llprim = new LLPrimitive(prim, scene, mesher);

            prim.Acceleration = obj.Acceleration;
            if (obj.AllowedDrop) prim.Flags |= PrimFlags.AllowInventoryDrop;
            prim.AngularVelocity = obj.AngularVelocity;
            llprim.AttachmentPosition = obj.AttachmentPosition;
            llprim.AttachmentRotation = obj.AttachmentRotation;
            llprim.BeforeAttachmentRotation = obj.BeforeAttachmentRotation;
            //obj.CameraAtOffset;
            //obj.CameraEyeOffset;
            prim.ClickAction = (ClickAction)obj.ClickAction;
            //obj.CollisionSound;
            //obj.CollisionSoundVolume;
            prim.Properties.CreationDate = obj.CreationDate;
            prim.Properties.CreatorID = obj.CreatorID;
            prim.Properties.Description = obj.Description;
            if (obj.DieAtEdge) prim.Flags |= PrimFlags.DieAtEdge;
            prim.Flexible = FromPrimObjectFlexible(obj.Flexible);
            prim.Properties.FolderID = obj.FolderID;
            prim.Properties.GroupID = obj.GroupID;
            prim.ID = obj.ID;
            llprim.Inventory = FromPrimObjectInventory(llprim, obj.Inventory);
            llprim.LastAttachmentPoint = (AttachmentPoint)obj.LastAttachmentPoint;
            prim.Properties.LastOwnerID = obj.LastOwnerID;
            prim.Light = FromPrimObjectLight(obj.Light);
            //obj.LinkNumber;
            prim.LocalID = obj.LocalID;
            prim.Properties.Name = obj.Name;
            prim.OwnerID = obj.OwnerID;
            //obj.ParentID;
            prim.ParticleSys = FromPrimObjectParticles(obj.Particles);
            prim.Properties.Permissions = new Permissions(obj.PermsBase, obj.PermsEveryone, obj.PermsGroup, obj.PermsNextOwner, obj.PermsOwner);
            if (obj.Phantom) prim.Flags |= PrimFlags.Phantom;
            prim.Position = obj.Position;
            prim.RegionHandle = Util.PositionToRegionHandle(scene.MinPosition);
            llprim.RemoteScriptAccessPIN = obj.RemoteScriptAccessPIN;
            if (obj.ReturnAtEdge) prim.Flags |= PrimFlags.ReturnAtEdge;
            //obj.RezDate;
            prim.Rotation = obj.Rotation;
            prim.Properties.SalePrice = obj.SalePrice;
            prim.Properties.SalePrice = obj.SaleType;
            if (obj.Sandbox) prim.Flags |= PrimFlags.Sandbox;
            prim.Scale = obj.Scale;
            //obj.ScriptState;
            prim.Sculpt = FromPrimObjectSculpt(obj.Sculpt);
            //obj.Selected;
            //obj.SelectorID;
            prim.PrimData = FromPrimObjectShape(obj.Shape, obj.PCode, obj.Material);
            prim.Properties.SitName = obj.SitName;
            llprim.SitPosition = obj.SitOffset;
            llprim.SitRotation = obj.SitRotation;
            prim.SoundFlags = (SoundFlags)obj.SoundFlags;
            prim.SoundGain = obj.SoundGain;
            prim.Sound = obj.SoundID;
            prim.SoundRadius = obj.SoundRadius;
            //obj.State;
            if (obj.Temporary) prim.Flags |= PrimFlags.Temporary;
            prim.Text = obj.Text;
            prim.TextColor = obj.TextColor;
            prim.Textures = obj.Textures;
            prim.Properties.TouchName = obj.TouchName;
            if (obj.UsePhysics) prim.Flags |= PrimFlags.Physics;
            prim.Velocity = obj.Velocity;
            //obj.VolumeDetect

            return llprim;
        }

        private static Primitive.FlexibleData FromPrimObjectFlexible(PrimObject.FlexibleBlock objFlex)
        {
            if (objFlex == null)
                return null;

            Primitive.FlexibleData flex = new Primitive.FlexibleData();
            flex.Drag = objFlex.Drag;
            flex.Force = objFlex.Force;
            flex.Gravity = objFlex.Gravity;
            flex.Softness = objFlex.Softness;
            flex.Tension = objFlex.Tension;
            flex.Wind = objFlex.Wind;

            return flex;
        }

        private static Primitive.LightData FromPrimObjectLight(PrimObject.LightBlock objLight)
        {
            if (objLight == null)
                return null;

            Primitive.LightData light = new Primitive.LightData();
            light.Color = objLight.Color;
            light.Cutoff = objLight.Cutoff;
            light.Falloff = objLight.Falloff;
            light.Intensity = objLight.Intensity;
            light.Radius = objLight.Radius;

            return light;
        }

        private static Primitive.SculptData FromPrimObjectSculpt(PrimObject.SculptBlock objSculpt)
        {
            if (objSculpt == null)
                return null;

            Primitive.SculptData sculpt = new Primitive.SculptData();
            sculpt.Type = (SculptType)objSculpt.Type;
            sculpt.SculptTexture = objSculpt.Texture;

            return sculpt;
        }

        private static Primitive.ParticleSystem FromPrimObjectParticles(PrimObject.ParticlesBlock objParticles)
        {
            Primitive.ParticleSystem particles = new Primitive.ParticleSystem();

            if (objParticles == null)
                return particles;
            
            particles.PartAcceleration = objParticles.Acceleration;
            particles.AngularVelocity = objParticles.AngularVelocity;
            particles.BurstPartCount = (byte)objParticles.BurstParticleCount;
            particles.BurstRadius = objParticles.BurstRadius;
            particles.BurstRate = objParticles.BurstRate;
            particles.BurstSpeedMax = objParticles.BurstSpeedMax;
            particles.BurstSpeedMin = objParticles.BurstSpeedMin;
            particles.PartDataFlags = (Primitive.ParticleSystem.ParticleDataFlags)objParticles.DataFlags;
            particles.PartFlags = (uint)objParticles.Flags;
            particles.InnerAngle = objParticles.InnerAngle;
            particles.MaxAge = objParticles.MaxAge;
            particles.OuterAngle = objParticles.OuterAngle;
            particles.PartEndColor = objParticles.ParticleEndColor;
            particles.PartEndScaleX = objParticles.ParticleEndScale.X;
            particles.PartEndScaleY = objParticles.ParticleEndScale.Y;
            particles.PartMaxAge = objParticles.ParticleMaxAge;
            particles.PartStartColor = objParticles.ParticleStartColor;
            particles.PartStartScaleX = objParticles.ParticleStartScale.X;
            particles.PartStartScaleY = objParticles.ParticleStartScale.Y;
            particles.Pattern = (Primitive.ParticleSystem.SourcePattern)objParticles.Pattern;
            particles.StartAge = objParticles.StartAge;
            particles.Target = objParticles.TargetID;
            particles.Texture = objParticles.TextureID;

            return particles;
        }

        private static Primitive.ConstructionData FromPrimObjectShape(PrimObject.ShapeBlock objShape, int pcode, int material)
        {
            Primitive.ConstructionData shape = new Primitive.ConstructionData();
            shape.PCode = (PCode)pcode;
            shape.Material = (Material)material;
            shape.PathBegin = objShape.PathBegin;
            shape.PathCurve = (PathCurve)objShape.PathCurve;
            shape.PathEnd = objShape.PathEnd;
            shape.PathRadiusOffset = objShape.PathRadiusOffset;
            shape.PathRevolutions = objShape.PathRevolutions;
            shape.PathScaleX = objShape.PathScaleX;
            shape.PathScaleY = objShape.PathScaleY;
            shape.PathShearX = objShape.PathShearX;
            shape.PathShearY = objShape.PathShearY;
            shape.PathSkew = objShape.PathSkew;
            shape.PathTaperX = objShape.PathTaperX;
            shape.PathTaperY = objShape.PathTaperY;
            shape.PathTwist = objShape.PathTwist;
            shape.PathTwistBegin = objShape.PathTwistBegin;
            shape.ProfileBegin = objShape.ProfileBegin;
            shape.ProfileCurve = (ProfileCurve)objShape.ProfileCurve;
            shape.ProfileEnd = objShape.ProfileEnd;
            shape.ProfileHollow = objShape.ProfileHollow;

            return shape;
        }

        private static PrimInventory FromPrimObjectInventory(LLPrimitive host, PrimObject.InventoryBlock objInv)
        {
            PrimInventory inv = new PrimInventory(host);

            if (objInv == null)
                return inv;

            for (int i = 0; i < objInv.Items.Length; i++)
            {
                PrimObject.InventoryBlock.ItemBlock objItem = objInv.Items[i];
                
                LLInventoryTaskItem item = new LLInventoryTaskItem();
                item.AssetID = objItem.AssetID;
                item.ContentType = LLUtil.LLAssetTypeToContentType((int)objItem.Type);
                item.CreationDate = objItem.CreationDate;
                item.CreatorID = objItem.CreatorID;
                item.Description = objItem.Description;
                item.Flags = (uint)objItem.Flags;
                item.GroupID = objItem.GroupID;
                item.ID = objItem.ID;
                item.Name = objItem.Name;
                item.OwnerID = objItem.OwnerID;
                item.Permissions = new Permissions(objItem.PermsBase, objItem.PermsEveryone, objItem.PermsGroup, objItem.PermsNextOwner, objItem.PermsOwner);
                item.PermissionGranter = objItem.PermsGranterID;

                inv.AddOrUpdateItem(item, true);
            }

            inv.InventorySerial = (short)objInv.Serial;
            return inv;
        }

        #endregion PrimObject Conversion
    }
}
