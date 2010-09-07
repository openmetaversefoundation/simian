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
using Simian.Protocols.Linden.Packets;

namespace Simian.Scripting.Linden
{
    public partial class LindenApi : ISceneModule, IScriptApi
    {
        [ScriptMethod]
        public string llGetDate(IScriptInstance script)
        {
            DateTime now = DateTime.Now;
            return String.Format("{0}-{1}-{2}", now.Year, now.Month, now.Date);
        }

        [ScriptMethod]
        public int llGetUnixTime(IScriptInstance script)
        {
            return (int)Utils.GetUnixTime();
        }

        [ScriptMethod]
        public float llGetWallclock(IScriptInstance script)
        {
            DateTime now = DateTime.Now;
            return (now.Hour * 3600) + (now.Minute * 60) + now.Second + (now.Millisecond / 1000);
        }

        [ScriptMethod]
        public int llGetRegionAgentCount(IScriptInstance script)
        {
            return script.Host.Scene.PresenceCount();
        }

        [ScriptMethod]
        public string llGetRegionName(IScriptInstance script)
        {
            return script.Host.Scene.Name;
        }

        [ScriptMethod]
        public string llKey2Name(IScriptInstance script, string id)
        {
            ISceneEntity entity;
            UUID key;
            if (UUID.TryParse(id, out key) && script.Host.Scene.TryGetEntity(key, out entity))
                return entity.Name;

            return String.Empty;
        }

        [ScriptMethod]
        public void llWhisper(IScriptInstance script, int channelID, string text)
        {
            Say(script, Chat.WHISPER_DIST, text, channelID, EntityChatType.Normal);
        }

        [ScriptMethod]
        public void llSay(IScriptInstance script, int channelID, string text)
        {
            Say(script, Chat.NORMAL_DIST, text, channelID, EntityChatType.Normal);
        }

        [ScriptMethod]
        public void llShout(IScriptInstance script, int channelID, string text)
        {
            Say(script, Chat.SHOUT_DIST, text, channelID, EntityChatType.Normal);
        }

        [ScriptMethod]
        public void llOwnerSay(IScriptInstance script, string text)
        {
            Say(script, 0f, text, 0, EntityChatType.Owner);
        }

        [ScriptMethod]
        public void llInstantMessage(IScriptInstance script, string user, string message)
        {
            if (m_messaging != null)
            {
                UUID toID;
                UUID.TryParse(user, out toID);

                // Keep a persistent messageID for all IMs from the host object to the target agent
                UUID messageID = UUID.Combine(script.Host.ID, toID);

                m_messaging.SendInstantMessage(messageID, toID, script.Host.Name, 
                    script.Host.ScenePosition, script.Host.Scene.ID, false, 
                    InstantMessageDialog.MessageFromObject, message, false, DateTime.UtcNow,
                    Utils.EmptyBytes);
            }

            script.AddSleepMS(2000);
        }

        [ScriptMethod]
        public void llRegionSay(IScriptInstance script, int channelID, string text)
        {
            // Cannot use llRegionSay on PUBLIC_CHANNEL: http://wiki.secondlife.com/wiki/LlRegionSay
            if (channelID == 0)
                return;
            
            Say(script, 0f, text, channelID, EntityChatType.Broadcast);
        }

        [ScriptMethod]
        public Vector3 llWind(IScriptInstance script, Vector3 offset)
        {
            return Vector3.Zero; // TODO: Wind?
        }

        [ScriptMethod]
        public Vector3 llGroundNormal(IScriptInstance script, Vector3 offset)
        {
            Vector3 pos = script.Host.ScenePosition + offset;

            // Clamp to valid position
            pos.X = Utils.Clamp(pos.X, 0f, 255f);
            pos.Y = Utils.Clamp(pos.Y, 0f, 255f);

            // Find three points to define a plane
            Vector3 p0 = new Vector3(pos.X, pos.Y, 0f);
            Vector3 p1 = new Vector3(pos.X + 1f, pos.Y, 0f);
            Vector3 p2 = new Vector3(pos.X, pos.Y + 1f, 0f);

            if (m_terrain != null)
            {
                float[] terrain = m_terrain.GetHeightmap();
                p0.Z = terrain[(int)pos.Y * 256 + (int)pos.X];

                p1.Z = (pos.X + 1f < 255f)
                    ? terrain[(int)pos.Y * 256 + (int)(pos.X + 1f)]
                    : p0.Z;

                p2.Z = (pos.Y + 1f < 255f)
                    ? terrain[(int)(pos.Y + 1f) * 256 + (int)pos.X]
                    : p0.Z;
            }

            // Find normalized vectors from p0 to p1 and p0 to p2
            Vector3 v0 = p1 - p0;
            Vector3 v1 = p2 - p0;
            v0.Normalize();
            v1.Normalize();

            // Find the cross product of the vectors (the slope normal)
            return Vector3.Cross(v0, v1);
        }

        [ScriptMethod]
        public Vector3 llGroundSlope(IScriptInstance script, Vector3 offset)
        {
            // Get the slope normal. This gives us the equation of the plane tangent to the slope
            Vector3 normal = llGroundNormal(script, offset);

            // Plug the x,y coordinates of the slope normal into the equation of the plane to get
            // the height of that point on the plane. The resulting vector gives the slope
            Vector3 slope = new Vector3(normal.X, normal.Y, ((normal.X * normal.X) + (normal.Y * normal.Y)) / (normal.Z * -1f));

            // Not sure if normalization is needed here
            slope.Normalize();

            return slope;
        }

        [ScriptMethod]
        public double llGround(IScriptInstance script, Vector3 offset)
        {
            Vector3 pos = script.Host.ScenePosition + offset;

            // Clamp to valid position
            pos.X = Utils.Clamp(pos.X, 0f, 255f);
            pos.Y = Utils.Clamp(pos.Y, 0f, 255f);

            //Get the slope normal.  This gives us the equation of the plane tangent to the slope.
            Vector3 normal = llGroundNormal(script, offset);

            // Get the height for the integer coordinates from the Heightmap
            float baseheight = (m_terrain != null)
                ? m_terrain.GetHeightmap()[(int)pos.Y * 256 + (int)pos.X]
                : 0f;

            // Calculate the difference between the actual coordinates and the integer coordinates
            double xdiff = pos.X - Math.Floor(pos.X);
            double ydiff = pos.Y - Math.Floor(pos.Y);

            // Use the equation of the tangent plane to adjust the height to account for slope
            return (((normal.X * xdiff) + (normal.Y * ydiff)) / (normal.Z  * -1f)) + baseheight;
        }

        [ScriptMethod]
        public Vector3 llGroundContour(IScriptInstance script, Vector3 offset)
        {
            Vector3 slope = llGroundSlope(script, offset);
            return new Vector3(-slope.Y, slope.X, 0f);
        }

        private void Say(IScriptInstance script, float audibleDist, string message, int channel, EntityChatType type)
        {
            const int DEBUG_CHANNEL = 0x7FFFFFFF;

            // Special handling for debug chat
            if (channel == DEBUG_CHANNEL)
            {
                type = EntityChatType.Debug;
                channel = 0;
            }

            // Truncate long messages
            if (message.Length > 1023)
                message = message.Substring(0, 1023);

            // Send this chat to the scene
            script.Host.Scene.EntityChat(this, script.Host, audibleDist, message, channel, type);
        }
    }
}
