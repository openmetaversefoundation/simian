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
using HttpServer;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    [SceneModule("ObjectMedia")]
    public class ObjectMedia : ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IHttpServer m_httpServer;
        private LLPermissions m_permissions;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_httpServer = m_scene.Simian.GetAppModule<IHttpServer>();
            if (m_httpServer == null)
            {
                m_log.Warn("ObjectMedia requires an IHttpServer");
                return;
            }

            m_permissions = m_scene.GetSceneModule<LLPermissions>();

            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "ObjectMedia", ObjectMediaHandler);
        }

        public void Stop()
        {
            if (m_httpServer != null)
            {
                m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "ObjectMedia");
            }
        }

        private void ObjectMediaHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            ObjectMediaMessage message;
            if (LLUtil.TryGetMessage<ObjectMediaMessage>(request.Body, out message))
            {
                if (message.Request is ObjectMediaRequest)
                {
                    ObjectMediaRequest mediaRequest = (ObjectMediaRequest)message.Request;

                    ISceneEntity entity;
                    if (m_scene.TryGetEntity(mediaRequest.PrimID, out entity) && entity is LLPrimitive)
                    {
                        LLPrimitive prim = (LLPrimitive)entity;

                        ObjectMediaResponse reply = new ObjectMediaResponse();
                        reply.PrimID = prim.ID;
                        reply.FaceMedia = prim.Prim.FaceMedia ?? new MediaEntry[0];
                        reply.Version = prim.Prim.MediaVersion ?? String.Empty;

                        LLUtil.SendLLSDXMLResponse(response, reply.Serialize());
                    }
                    else
                    {
                        m_log.Warn("Received an ObjectMedia request for unknown prim " + mediaRequest.PrimID);
                    }
                }
                else if (message.Request is ObjectMediaUpdate)
                {
                    ObjectMediaUpdate update = (ObjectMediaUpdate)message.Request;

                    ISceneEntity entity;
                    if (m_scene.TryGetEntity(update.PrimID, out entity) && entity is LLPrimitive)
                    {
                        LLPrimitive prim = (LLPrimitive)entity;
                        int lastVersion = ParseMediaVersion(prim.Prim.MediaVersion);

                        prim.Prim.FaceMedia = update.FaceMedia;
                        prim.Prim.MediaVersion = CreateMediaVersion(lastVersion + 1, cap.OwnerID);

                        // Set the CurrentURL fields
                        for (int i = 0; i < prim.Prim.FaceMedia.Length; i++)
                        {
                            MediaEntry entry = prim.Prim.FaceMedia[i];
                            if (entry != null && String.IsNullOrEmpty(entry.CurrentURL))
                                entry.CurrentURL = entry.HomeURL;
                        }

                        // Set the texture face media flags
                        for (int i = 0; i < prim.Prim.Textures.FaceTextures.Length; i++)
                        {
                            Primitive.TextureEntryFace face = prim.Prim.Textures.FaceTextures[i];
                            MediaEntry entry = (update.FaceMedia.Length > i) ? update.FaceMedia[i] : null;

                            if (entry != null)
                            {
                                if (face == null)
                                    face = prim.Prim.Textures.CreateFace((uint)i);

                                face.MediaFlags = true;
                            }
                            else if (face != null)
                            {
                                face.MediaFlags = false;
                            }
                        }

                        m_scene.EntityAddOrUpdate(this, prim, UpdateFlags.FullUpdate, 0);
                    }
                    else
                    {
                        m_log.Warn("Could not update ObjectMedia for " + update.PrimID + ", prim not found");
                    }
                }
                else
                {
                    m_log.Warn("Unrecognized ObjectMedia message: " + message.Request);
                }
            }
            else
            {
                m_log.Warn("Received invalid data for ObjectMedia");
                response.Status = System.Net.HttpStatusCode.BadRequest;
            }
        }

        private static string CreateMediaVersion(int version, UUID lastEditorID)
        {
            return "x-mv:" + version.ToString("D10") + "/" + lastEditorID.ToString();
        }

        private static int ParseMediaVersion(string mediaVersion)
        {
            int version = 0;

            if (!String.IsNullOrEmpty(mediaVersion) && mediaVersion.StartsWith("x-mv:") && mediaVersion.Contains("/"))
                Int32.TryParse(mediaVersion.Substring(5, mediaVersion.IndexOf('/') - 5), out version);

            return version;
        }
    }
}
