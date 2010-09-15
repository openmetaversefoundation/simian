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
    [SceneModule("GetMesh")]
    public class GetMesh : ISceneModule
    {
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
                m_log.Warn("GetMesh requires an IHttpServer");
                return;
            }

            m_assetClient = m_scene.Simian.GetAppModule<IAssetClient>();
            if (m_assetClient == null)
            {
                m_log.Warn("GetMesh requires an IAssetClient");
                return;
            }

            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "GetMesh", GetMeshHandler);
        }

        public void Stop()
        {
            if (m_httpServer != null)
            {
                m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "GetMesh");
            }
        }

        private void GetMeshHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            // TODO: Change this to a config option
            const string REDIRECT_URL = null;

            // Try to parse the mesh ID from the request URL
            NameValueCollection query = HttpUtility.ParseQueryString(request.Uri.Query);
            string meshStr = query.GetOne("mesh_id");

            UUID meshID;
            if (!String.IsNullOrEmpty(meshStr) && UUID.TryParse(meshStr, out meshID))
            {
                Asset mesh;

                if (!String.IsNullOrEmpty(REDIRECT_URL))
                {
                    // Only try to fetch locally cached meshes. Misses are redirected
                    if (m_assetClient.TryGetCachedAsset(meshID, "application/vnd.ll.mesh", out mesh))
                    {
                        SendMesh(request, response, mesh);
                    }
                    else
                    {
                        string meshUrl = REDIRECT_URL + meshID.ToString();
                        m_log.Debug("Redirecting mesh request to " + meshUrl);
                        response.Redirect(meshUrl);
                    }
                }
                else
                {
                    // Fetch locally or remotely. Misses return a 404
                    if (m_assetClient.TryGetAsset(meshID, "application/vnd.ll.mesh", out mesh))
                    {
                        SendMesh(request, response, mesh);
                    }
                    else
                    {
                        m_log.Warn("Mesh " + meshID + " not found, returning a 404");
                        response.Status = System.Net.HttpStatusCode.NotFound;
                    }
                }
            }
            else
            {
                m_log.Warn("Failed to parse a mesh_id from GetMesh request: " + request.Uri);
            }
        }

        private void SendMesh(IHttpRequest request, IHttpResponse response, Asset asset)
        {
            // TODO: Enable this again when we confirm the viewer is properly handling partial mesh content
            string range = null; //request.Headers.GetOne("Range");
            if (!String.IsNullOrEmpty(range))
            {
                // Range request
                int start, end;
                if (TryParseRange(range, out start, out end))
                {
                    end = Utils.Clamp(end, 1, asset.Data.Length);
                    start = Utils.Clamp(start, 0, end - 1);

                    //m_log.Debug("Serving " + start + " to " + end + " of " + asset.Data.Length + " bytes for mesh " + asset.ID);

                    if (end - start < asset.Data.Length)
                        response.Status = System.Net.HttpStatusCode.PartialContent;

                    response.ContentLength = end - start;
                    response.ContentType = asset.ContentType;

                    response.Body.Write(asset.Data, start, end - start);
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
                response.ContentLength = asset.Data.Length;
                response.ContentType = asset.ContentType;
                response.Body.Write(asset.Data, 0, asset.Data.Length);
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
