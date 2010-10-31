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
using System.ComponentModel.Composition;
using System.Collections.Generic;
using HttpServer;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;

namespace Simian.Protocols.Linden
{
    [SceneModule("Upload")]
    public class Upload : ISceneModule
    {
        private const byte MEDIA_MASK = 0x01;
        private const byte TEX_MAP_MASK = 0x06;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IHttpServer m_httpServer;
        private IAssetClient m_assetClient;
        private IPrimMesher m_primMesher;
        private LLPermissions m_permissions;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_assetClient = m_scene.Simian.GetAppModule<IAssetClient>();
            if (m_assetClient == null)
            {
                m_log.Warn("Upload requires an IAssetClient");
                return;
            }

            m_httpServer = m_scene.Simian.GetAppModule<IHttpServer>();
            if (m_httpServer == null)
            {
                m_log.Warn("Upload requires an IHttpServer");
                return;
            }

            m_primMesher = m_scene.GetSceneModule<IPrimMesher>();
            m_permissions = m_scene.GetSceneModule<LLPermissions>();

            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "UploadBakedTexture", UploadBakedTextureHandler);
            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "UploadBakedTextureData", UploadBakedTextureDataHandler);
            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "UploadObjectAsset", UploadObjectAssetHandler);
        }

        public void Stop()
        {
            if (m_httpServer != null)
            {
                m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "UploadBakedTexture");
                m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "UploadBakedTextureData");
                m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "UploadObjectAsset");
            }
        }

        private void UploadBakedTextureHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            UploadBakedTextureMessage reply = new UploadBakedTextureMessage();
            UploaderRequestUpload replyBlock = new UploaderRequestUpload();

            // Create a temporary uploader capability
            replyBlock.Url = m_scene.Capabilities.AddCapability(cap.OwnerID, true, m_scene.ID, "UploadBakedTextureData");
            reply.Request = replyBlock;

            m_log.Debug("Created baked texture upload capability " + replyBlock.Url + " for " + cap.OwnerID);

            LLUtil.SendLLSDXMLResponse(response, reply.Serialize());
        }

        private void UploadBakedTextureDataHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            byte[] textureData = request.GetBody();
            UUID assetID = UUID.Zero;

            m_log.Debug("Received baked texture upload from " + cap.OwnerID + " (" + textureData.Length + " bytes)");

            if (textureData != null && textureData.Length > 0)
            {
                if (!m_assetClient.StoreAsset("image/x-j2c", true, true, textureData, cap.OwnerID, out assetID))
                    m_log.WarnFormat("Failed to store uploaded texture bake ({0} bytes)", textureData.Length);
            }
            else
            {
                m_log.Warn("Texture bake upload contained no data");
            }

            UploadBakedTextureMessage reply = new UploadBakedTextureMessage();
            UploaderRequestComplete replyBlock = new UploaderRequestComplete();
            replyBlock.AssetID = assetID;
            reply.Request = replyBlock;

            LLUtil.SendLLSDXMLResponse(response, reply.Serialize());
        }

        private void UploadObjectAssetHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            UploadObjectAssetMessage message;
            if (LLUtil.TryGetMessage<UploadObjectAssetMessage>(request.Body, out message))
            {
                LLPrimitive parent = null;

                // Build the linkset
                for (int i = 0; i < message.Objects.Length; i++)
                {
                    UploadObjectAssetMessage.Object obj = message.Objects[i];

                    #region Primitive Creation

                    Primitive prim = new Primitive();
                    prim.Properties = new Primitive.ObjectProperties();
                    prim.Sculpt = new Primitive.SculptData();
                    prim.Textures = new Primitive.TextureEntry(UUID.Zero);

                    prim.Flags = PrimFlags.CastShadows | PrimFlags.InventoryEmpty | PrimFlags.CreateSelected;
                    prim.ID = UUID.Random();
                    prim.MediaURL = String.Empty;
                    prim.OwnerID = cap.OwnerID;
                    prim.GroupID = obj.GroupID;
                    prim.TextColor = Color4.Black;

                    prim.Properties.CreationDate = DateTime.UtcNow;
                    prim.Properties.CreatorID = cap.OwnerID;
                    prim.Properties.Description = String.Empty;
                    prim.Properties.GroupID = obj.GroupID;
                    prim.Properties.LastOwnerID = cap.OwnerID;
                    prim.Properties.Name = obj.Name;
                    prim.Properties.ObjectID = prim.ID;
                    prim.Properties.OwnerID = prim.OwnerID;
                    prim.Properties.Permissions = (m_permissions != null) ?
                        m_permissions.GetDefaultPermissions() :
                        Permissions.FullPermissions;
                    prim.Properties.SalePrice = 10;

                    prim.PrimData.PCode = PCode.Prim;
                    prim.PrimData.Material = obj.Material;
                    prim.PrimData.PathBegin = obj.PathBegin;
                    prim.PrimData.PathCurve = (PathCurve)obj.PathCurve;
                    prim.PrimData.PathEnd = obj.PathEnd;
                    prim.PrimData.ProfileBegin = obj.ProfileBegin;
                    prim.PrimData.ProfileCurve = (ProfileCurve)obj.ProfileCurve;
                    prim.PrimData.ProfileEnd = obj.ProfileEnd;
                    prim.PrimData.ProfileHollow = obj.ProfileHollow;
                    prim.PrimData.PathRadiusOffset = obj.RadiusOffset;
                    prim.PrimData.PathRevolutions = obj.Revolutions;
                    prim.PrimData.PathScaleX = obj.ScaleX;
                    prim.PrimData.PathScaleY = obj.ScaleY;
                    prim.PrimData.PathShearX = obj.ShearX;
                    prim.PrimData.PathShearY = obj.ShearY;
                    prim.PrimData.PathSkew = obj.Skew;
                    prim.PrimData.PathTaperX = obj.TaperX;
                    prim.PrimData.PathTaperY = obj.TaperY;
                    prim.PrimData.PathTwist = obj.Twist;
                    prim.PrimData.PathTwistBegin = obj.TwistBegin;

                    // Extra parameters
                    for (int j = 0; j < obj.ExtraParams.Length; j++)
                    {
                        UploadObjectAssetMessage.Object.ExtraParam extraParam = obj.ExtraParams[j];

                        switch (extraParam.Type)
                        {
                            case ExtraParamType.Flexible:
                                prim.Flexible = new Primitive.FlexibleData(extraParam.ExtraParamData, 0);
                                break;
                            case ExtraParamType.Light:
                                prim.Light = new Primitive.LightData(extraParam.ExtraParamData, 0);
                                break;
                            case ExtraParamType.Sculpt:
                                prim.Sculpt = new Primitive.SculptData(extraParam.ExtraParamData, 0);
                                break;
                        }
                    }

                    // Faces
                    for (int j = 0; j < obj.Faces.Length; j++)
                    {
                        UploadObjectAssetMessage.Object.Face face = obj.Faces[j];

                        Primitive.TextureEntryFace primFace = prim.Textures.GetFace(0);
                        primFace.Bump = face.Bump;
                        primFace.RGBA = face.Color;
                        primFace.Fullbright = face.Fullbright;
                        primFace.Glow = face.Glow;
                        primFace.TextureID = face.ImageID;
                        primFace.Rotation = face.ImageRot;
                        primFace.MediaFlags = ((face.MediaFlags & MEDIA_MASK) != 0);

                        primFace.OffsetU = face.OffsetS;
                        primFace.OffsetV = face.OffsetT;
                        primFace.RepeatU = face.ScaleS;
                        primFace.RepeatV = face.ScaleT;
                        primFace.TexMapType = (MappingType)(face.MediaFlags & TEX_MAP_MASK);
                    }

                    prim.Sculpt.SculptTexture = obj.SculptID;
                    prim.Sculpt.Type = obj.SculptType;

                    #endregion Primitive Creation

                    LLPrimitive llprim = new LLPrimitive(prim, m_scene, m_primMesher);
                    llprim.Scale = obj.Scale;

                    // Link children prims to the parent
                    if (i == 0)
                    {
                        llprim.RelativePosition = obj.Position;
                        llprim.RelativeRotation = obj.Rotation;
                        m_scene.EntityAddOrUpdate(this, llprim, UpdateFlags.FullUpdate, 0);

                        parent = llprim;
                    }
                    else
                    {
                        llprim.RelativePosition = obj.Position;
                        llprim.RelativeRotation = obj.Rotation;
                        llprim.SetParent(parent, true, false);
                        m_scene.EntityAddOrUpdate(this, llprim, UpdateFlags.FullUpdate, 0);
                    }
                }
            }
            else
            {
                m_log.Warn("Received invalid data for UploadObjectAsset");
                response.Status = System.Net.HttpStatusCode.BadRequest;
            }
        }
    }
}
