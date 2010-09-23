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
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian
{
    [System.Diagnostics.DebuggerDisplay("{Name} ({ID})")]
    public class SceneInfo
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        public UUID ID;
        public string Name;
        public Vector3d MinPosition;
        public Vector3d MaxPosition;
        public Uri PublicSeedCapability;

        private Dictionary<string, Uri> m_publicCapabilities;
        private object m_syncRoot = new object();

        public Vector3d GlobalPosition { get { return (MaxPosition + MinPosition) * 0.5d; } }

        public bool TryGetCapability(string cap, out Uri address)
        {
            lock (m_syncRoot)
            {
                if (m_publicCapabilities == null)
                    FetchCapabilities();

                return m_publicCapabilities.TryGetValue(cap, out address);
            }
        }

        private void FetchCapabilities()
        {
            m_publicCapabilities = new Dictionary<string, Uri>();

            if (PublicSeedCapability != null)
            {
                OSDMap responseMap = WebUtil.GetService(PublicSeedCapability.AbsoluteUri);

                if (responseMap.ContainsKey("capabilities") && responseMap["capabilities"].Type == OSDType.Map)
                {
                    OSDMap caps = (OSDMap)responseMap["capabilities"];

                    foreach (KeyValuePair<string, OSD> kvp in caps)
                    {
                        Uri capUri = kvp.Value.AsUri();
                        if (capUri != null)
                            m_publicCapabilities[kvp.Key] = capUri;
                        else
                            m_log.Warn("Ignoring unrecognized capability format: <" + kvp.Key + "," + kvp.Value.ToString() + ">");
                    }
                }
                else
                {
                    m_log.Warn("Public seed capability fetch from " + Name + " failed: " + responseMap["Message"].AsString());
                }
            }
            else
            {
                m_log.Warn("Can't fetch capabilities for scene " + Name + ", no public seed capability");
            }
        }

        public static SceneInfo FromScene(IScene scene)
        {
            SceneInfo info = new SceneInfo
            {
                ID = scene.ID,
                Name = scene.Name,
                MinPosition = scene.MinPosition,
                MaxPosition = scene.MaxPosition
            };
            scene.TryGetPublicCapability("public_region_seed_capability", out info.PublicSeedCapability);

            return info;
        }
    }
}
