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
using OpenMetaverse;

namespace Simian.Protocols.Linden
{
    #region Enums

    /// <summary>
    /// LSL event types
    /// </summary>
    [Flags]
    public enum LSLEventFlags
    {
        attach = 1 << 0,
        collision = 1 << 1,
        collision_end = 1 << 2,
        collision_start = 1 << 3,
        control = 1 << 4,
        dataserver = 1 << 5,
        email = 1 << 6,
        http_response = 1 << 7,
        land_collision = 1 << 8,
        land_collision_end = 1 << 9,
        land_collision_start = 1 << 10,
        at_target = 1 << 11,
        at_rot_target = 1 << 12,
        listen = 1 << 13,
        money = 1 << 14,
        moving_end = 1 << 15,
        moving_start = 1 << 16,
        not_at_rot_target = 1 << 17,
        not_at_target = 1 << 18,
        remote_data = 1 << 19,
        run_time_permissions = 1 << 20,
        state_entry = 1 << 21,
        state_exit = 1 << 22,
        timer = 1 << 23,
        touch = 1 << 24,
        touch_end = 1 << 25,
        touch_start = 1 << 26,
        object_rez = 1 << 27
    }

    public static class LSLEventFlagsExtensions
    {
        public static bool HasFlag(this LSLEventFlags eventFlags, LSLEventFlags flag)
        {
            return (eventFlags & flag) == flag;
        }
    }

    #endregion Enums

    #region Scripting Support Classes

    /// <summary>
    /// Holds all the data required to execute a scripting event
    /// </summary>
    public class EventParams
    {
        public UUID ScriptID;
        public string EventName;
        public object[] Params;
        public DetectParams[] DetectParams;

        public EventParams(UUID scriptID, string eventName, object[] eventParams, DetectParams[] detectParams)
        {
            ScriptID = scriptID;
            EventName = eventName;
            Params = eventParams;
            DetectParams = detectParams;
        }
    }

    /// <summary>
    /// Holds all of the data a script can detect about the containing object
    /// </summary>
    public class DetectParams
    {
        public const int TOUCH_INVALID_FACE = -1;
        public static readonly Vector3 TOUCH_INVALID_TEXCOORD = new Vector3(-1.0f, -1.0f, 0.0f);

        public UUID Key;
        public int LinkNum;
        public UUID Group;
        public string Name = String.Empty;
        public UUID Owner;
        public Vector3 Offset;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.Identity;
        public Vector3 Velocity;
        public int Type;
        public Vector3 TouchST = TOUCH_INVALID_TEXCOORD;
        public Vector3 TouchNormal;
        public Vector3 TouchBinormal;
        public Vector3 TouchPos;
        public Vector3 TouchUV = TOUCH_INVALID_TEXCOORD;
        public int TouchFace = TOUCH_INVALID_FACE;
    }

    #endregion Scripting Support Classes

    public interface ILSLScriptEngine
    {
        bool RezScript(UUID sourceItemID, UUID sourceAssetID, ISceneEntity hostObject, int startParam);
        void StopScript(UUID scriptID);
        bool IsScriptRunning(UUID scriptID);

        bool PostScriptEvent(EventParams parms);
        bool PostObjectEvent(UUID hostObjectID, string eventName, object[] eventParams, DetectParams[] detectParams);

        void SetTimerEvent(UUID scriptID, double seconds);

        DetectParams GetDetectParams(UUID scriptID, int detectIndex);

        void SetStartParameter(UUID scriptID, int startParam);
        int GetStartParameter(UUID scriptID);

        LSLEventFlags GetEventsForState(UUID scriptID, string state);

        void SetScriptMinEventDelay(UUID scriptID, double minDelay);

        void TriggerState(UUID scriptID, string newState);

        void ApiResetScript(UUID scriptID);
        void ResetScript(UUID scriptID);

        int AddListener(UUID scriptID, UUID hostObjectID, int channel, string name, UUID keyID, string message);
        void RemoveListener(UUID scriptID, int handle);
        void RemoveListeners(UUID scriptID);
        void SetListenerState(UUID scriptID, int handle, bool enabled);

        void SensorOnce(UUID scriptID, UUID hostObjectID, string name, UUID keyID, int type, double range, double arc);
        void SensorRepeat(UUID scriptID, UUID hostObjectID, string name, UUID keyID, int type, double range, double arc, double rate);
        void SensorRemove(UUID scriptID);
    }
}
