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
using System.Diagnostics;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace Simian.Protocols.Linden.Packets
{
    [SceneModule("Sounds")]
    public class Sounds : ISceneModule
    {
        const string PRELOAD_SOUND = "PreloadSound";
        /// <summary>Magic UUID for combining with a sound ID to create an event ID for preloading
        /// sounds</summary>
        static readonly UUID PRELOAD_EVENT_ID = new UUID("bfd964ab-9ea4-4924-bc90-62b6137054d0");

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private LLUDP m_udp;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_scene.AddInterestListHandler(PRELOAD_SOUND, new InterestListEventHandler { SendCallback = SendPreloadSoundsPacket });
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
            }
        }

        public void PreloadSound(ISceneEntity source, UUID soundID, float radius)
        {
            m_scene.CreateInterestListEvent(new InterestListEvent(
                UUID.Combine(soundID, PRELOAD_EVENT_ID),
                PRELOAD_SOUND,
                source.ScenePosition,
                new Vector3(radius),
                new object[] { source, soundID })
            );
        }

        private void SendPreloadSoundsPacket(QueuedInterestListEvent[] eventDatas, IScenePresence presence)
        {
            if (!(presence is LLAgent) || presence.InterestList == null)
                return;
            LLAgent agent = (LLAgent)presence;

            PreloadSoundPacket preload = new PreloadSoundPacket();
            preload.DataBlock = new PreloadSoundPacket.DataBlockBlock[eventDatas.Length];

            for (int i = 0; i < eventDatas.Length; i++)
            {
                object[] state = (object[])eventDatas[i].Event.State;
                ISceneEntity source = (ISceneEntity)state[0];
                UUID soundID = (UUID)state[1];

                preload.DataBlock[i] = new PreloadSoundPacket.DataBlockBlock
                    { ObjectID = source.ID, OwnerID = source.OwnerID, SoundID = soundID };
            }

            m_udp.SendPacket(agent, preload, ThrottleCategory.Task, true);
        }
    }
}
