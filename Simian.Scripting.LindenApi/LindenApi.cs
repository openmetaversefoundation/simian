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
using System.ComponentModel.Composition;
using log4net;
using OpenMetaverse;
using Simian.Protocols.Linden;
using Simian.Protocols.Linden.Packets;

using Sounds = Simian.Protocols.Linden.Packets.Sounds;

namespace Simian.Scripting.Linden
{
    /// <summary>
    /// Contains all of the functions exposed through the LSL API
    /// </summary>
    /// <remarks>This partial class is spread out over several source files</remarks>
    [SceneModule("LindenApi")]
    public partial class LindenApi : ISceneModule, IScriptApi
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IHttpServer m_httpServer;
        private IAssetClient m_assetClient;
        private ITerrain m_terrain;
        private IPrimMesher m_primMesher;
        private ILSLScriptEngine m_lslScriptEngine;
        private Messaging m_messaging;
        private Sounds m_sounds;

        public void Start(IScene scene)
        {
            m_httpServer = scene.Simian.GetAppModule<IHttpServer>();
            m_assetClient = scene.Simian.GetAppModule<IAssetClient>();
            m_terrain = scene.GetSceneModule<ITerrain>();
            m_primMesher = scene.GetSceneModule<IPrimMesher>();
            m_lslScriptEngine = scene.GetSceneModule<ILSLScriptEngine>();
            m_messaging = scene.GetSceneModule<Messaging>();
            m_sounds = scene.GetSceneModule<Sounds>();
        }

        public void Stop()
        {
        }
    }
}
