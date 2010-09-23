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
using OpenMetaverse;
using Simian.Protocols.Linden;

namespace Simian.Scripting.Linden
{
    public partial class LindenApi : ISceneModule, IScriptApi
    {
        private const float DEFAULT_SOUND_RADIUS = 20f;

        //llAdjustSoundVolume

        //llCollisionSound

        [ScriptMethod]
        public void llLoopSound(IScriptInstance script, string sound, float volume)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return;

            if (prim.Prim.Sound != UUID.Zero)
                llStopSound(script);

            UUID soundID = KeyOrName(script, sound, AssetType.Sound);
            if (soundID != UUID.Zero)
            {
                prim.Prim.Sound = soundID;
                prim.Prim.SoundGain = Utils.Clamp(volume, 0f, 1f);
                prim.Prim.SoundFlags = SoundFlags.Loop;
                prim.Prim.SoundRadius = DEFAULT_SOUND_RADIUS;

                prim.Scene.EntityAddOrUpdate(this, prim, 0, (uint)LLUpdateFlags.Sound);
            }
        }

        //llLoopSoundMaster

        //llLoopSoundSlave

        //llPlaySound

        //llPlaySoundSlave

        [ScriptMethod]
        public void llPreloadSound(IScriptInstance script, string sound)
        {
            UUID soundID = KeyOrName(script, sound, AssetType.Sound);
            if (soundID == UUID.Zero)
            {
                script.Host.Scene.EntityChat(this, script.Host, 0f, "Cannot find sound " + sound, Int32.MaxValue, EntityChatType.Debug);
                return;
            }

            if (m_sounds != null)
                m_sounds.PreloadSound(script.Host, soundID, DEFAULT_SOUND_RADIUS);

            script.AddSleepMS(1000);
        }

        //llSetSoundQueueing

        //llSetSoundRadius

        [ScriptMethod]
        public void llStopSound(IScriptInstance script)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return;

            if (prim.Prim.Sound != UUID.Zero)
            {
                prim.Prim.Sound = UUID.Zero;
                prim.Prim.SoundGain = 0f;
                prim.Prim.SoundFlags = SoundFlags.None;
                prim.Prim.SoundRadius = 0f;

                prim.Scene.EntityAddOrUpdate(this, prim, 0, (uint)LLUpdateFlags.Sound);
            }
        }

        //llTriggerSound

        //llTriggerSoundLimited
    }
}
