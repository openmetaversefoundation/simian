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
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Web;
using HttpServer;
using log4net;
using OpenMetaverse;

namespace Simian.Protocols.Linden
{
    [SceneModule("GetTexture")]
    public class GetTexture : ISceneModule
    {
        private static readonly UUID MISSING_IMAGE = new UUID("32dfd1c8-7ff6-5909-d983-6d4adfb4255d");
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IHttpServer m_httpServer;
        private IAssetClient m_assetClient;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_httpServer = m_scene.Simian.GetAppModule<IHttpServer>();
            if (m_httpServer == null)
            {
                m_log.Warn("GetTexture requires an IHttpServer");
                return;
            }

            m_assetClient = m_scene.Simian.GetAppModule<IAssetClient>();
            if (m_assetClient == null)
            {
                m_log.Warn("GetTexture requires an IAssetClient");
                return;
            }

            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "GetTexture", GetTextureHandler);
        }

        public void Stop()
        {
            if (m_httpServer != null)
            {
                m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "GetTexture");
            }
        }

        private void GetTextureHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            // TODO: Change this to a config option
            const string REDIRECT_URL = null;

            // Try to parse the texture ID from the request URL
            NameValueCollection query = HttpUtility.ParseQueryString(request.Uri.Query);
            string textureStr = query.GetOne("texture_id");

            UUID textureID;
            if (!String.IsNullOrEmpty(textureStr) && UUID.TryParse(textureStr, out textureID))
            {
                Asset texture;

                if (!String.IsNullOrEmpty(REDIRECT_URL))
                {
                    // Only try to fetch locally cached textures. Misses are redirected
                    if (m_assetClient.TryGetCachedAsset(textureID, "image/x-j2c", out texture))
                    {
                        SendTexture(request, response, texture);
                    }
                    else
                    {
                        string textureUrl = REDIRECT_URL + textureID.ToString();
                        m_log.Debug("Redirecting texture request to " + textureUrl);
                        response.Redirect(textureUrl);
                    }
                }
                else
                {
                    // Fetch locally or remotely. Misses return a 404
                    if (m_assetClient.TryGetAsset(textureID, "image/x-j2c", out texture))
                    {
                        SendTexture(request, response, texture);
                    }
                    else
                    {
                        m_log.Warn("Texture " + textureID + " not found");

                        if (m_assetClient.TryGetCachedAsset(MISSING_IMAGE, "image/x-j2c", out texture))
                        {
                            SendTexture(request, response, texture);
                        }
                        else
                        {
                            m_log.Warn("Missing image texture " + MISSING_IMAGE + " not found, returning a 404");
                            response.Status = System.Net.HttpStatusCode.NotFound;
                        }
                    }
                }
            }
            else
            {
                m_log.Warn("Failed to parse a texture_id from GetTexture request: " + request.Uri);
            }
        }

        private void SendTexture(IHttpRequest request, IHttpResponse response, Asset texture)
        {
            string range = request.Headers.GetOne("Range");
            if (!String.IsNullOrEmpty(range))
            {
                // Range request
                int start, end;
                if (TryParseRange(range, out start, out end))
                {
                    end = Utils.Clamp(end, 1, texture.Data.Length);
                    start = Utils.Clamp(start, 0, end - 1);

                    //m_log.Debug("Serving " + start + " to " + end + " of " + texture.Data.Length + " bytes for texture " + texture.ID);

                    if (end - start < texture.Data.Length)
                        response.Status = System.Net.HttpStatusCode.PartialContent;

                    response.ContentLength = end - start;
                    response.ContentType = texture.ContentType;

                    response.Body.Write(texture.Data, start, end - start);
                }
                else
                {
                    m_log.Warn("Malformed Range header: " + range);
                    response.Status = System.Net.HttpStatusCode.BadRequest;
                }
            }
            else
            {
                // Full content request
                response.ContentLength = texture.Data.Length;
                response.ContentType = texture.ContentType;
                response.Body.Write(texture.Data, 0, texture.Data.Length);
            }
        }

        private bool TryParseRange(string header, out int start, out int end)
        {
            if (header.StartsWith("bytes="))
            {
                string[] rangeValues = header.Substring(6).Split('-');
                if (rangeValues.Length == 2)
                {
                    if (Int32.TryParse(rangeValues[0], out start) && Int32.TryParse(rangeValues[1], out end))
                        return true;
                }
            }

            start = end = 0;
            return false;
        }
    }
}
