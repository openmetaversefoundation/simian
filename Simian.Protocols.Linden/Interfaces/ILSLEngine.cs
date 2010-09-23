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
    public enum LSLEventFlags : long
    {
        /// <summary>Triggered on any state transition and startup</summary>
        state_entry = 1 << 0,
        /// <summary>Triggered on a qualifying state transition.</summary>
        state_exit = 1 << 1,
        /// <summary>Triggered by the start of an agent clicking the script's container</summary>
        touch_start = 1 << 2,
        /// <summary>Triggered while an agent is clicking script's container. It will continue 
        /// to be triggered until the the agent releases the click (it triggers multiple times)</summary>
        touch = 1 << 3,
        /// <summary>Triggered when an agent finishes clicking on the script's container</summary>
        touch_end = 1 << 4,
        /// <summary>Triggered when the script's container starts colliding with another object</summary>
        collision_start = 1 << 5,
        /// <summary>Triggered while the script's container is colliding with another object (it 
        /// triggers multiple times)</summary>
        collision = 1 << 6,
        /// <summary>Triggered when the script's container stops colliding with another object</summary>
        collision_end = 1 << 7,
        /// <summary>Triggered when the script's container starts colliding with land</summary>
        land_collision_start = 1 << 8,
        /// <summary>Triggered while the script's container is colliding with land (it triggers 
        /// multiple times)</summary>
        land_collision = 1 << 9,
        /// <summary>Triggered when the script's container stops colliding with land</summary>
        land_collision_end = 1 << 10,
        /// <summary>Timer-based event. Result of the llSetTimerEvent library function call</summary>
        timer = 1 << 11,
        /// <summary>Trigged by chat, use llListen to enable and filter</summary>
        listen = 1 << 12,
        /// <summary>Triggered when the script's container is rezzed (by script or by user). Also 
        /// triggered in attachments when a user logs in, or when the object is attached from 
        /// inventory</summary>
        on_rez = 1 << 13,
        /// <summary>Result from a call to llSensor or llSensorRepeat</summary>
        sensor = 1 << 14,
        /// <summary>Result from a call to llSensor or llSensorRepeat</summary>
        no_sensor = 1 << 15,
        /// <summary>Result of llTakeControls library function call and user input</summary>
        control = 1 << 16,
        /// <summary>Triggered when money is paid to the script's container</summary>
        money = 1 << 17,
        /// <summary>Triggered as a result of calling llGetNextEmail where there is a matching 
        /// email in the email queue</summary>
        email = 1 << 18,
        /// <summary>Result of llTarget library function call</summary>
        at_target = 1 << 19,
        /// <summary>Result of llTarget library function call</summary>
        not_at_target = 1 << 20,
        /// <summary>Result of llRotTarget library function call</summary>
        at_rot_target = 1 << 21,
        /// <summary>Result of llRotTarget library function call</summary>
        not_at_rot_target = 1 << 22,
        /// <summary>Triggered when an agent grants run time permissions to the script's container</summary>
        run_time_permissions = 1 << 23,
        /// <summary>Various changes to the script's container trigger this event</summary>
        changed = 1 << 24,
        /// <summary>Triggered when the script's container is attached or detached from an agent</summary>
        attach = 1 << 25,
        /// <summary>Triggered when the script receives an asynchronous data callback</summary>
        dataserver = 1 << 26,
        /// <summary>Triggered when the script receives a link message that was sent by a call to 
        /// llMessageLinked. llMessageLinked is used to send messages from one script to another in
        /// the same linkset</summary>
        link_message = 1 << 27,
        /// <summary>Triggered when the script's container begins moving</summary>
        moving_start = 1 << 28,
        /// <summary>Triggered when the script's container stops moving</summary>
        moving_end = 1 << 29,
        /// <summary>Triggered when the script's container rezzes another object</summary>
        object_rez = 1 << 30,
        /// <summary>Triggered by various XML-RPC calls</summary>
        remote_data = 1 << 31,
        /// <summary>Triggered when the script receives a response to an llHTTPRequest</summary>
        http_response = 1 << 32
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
        void ResetScript(UUID scriptID);
        void StopScript(UUID scriptID);
        bool IsScriptRunning(UUID scriptID);

        bool PostScriptEvent(EventParams parms);
        bool PostObjectEvent(UUID hostObjectID, string eventName, object[] eventParams, DetectParams[] detectParams);

        // FIXME: None of these should be API methods. Script engines should handle them internally
        void SetTimerEvent(UUID scriptID, double seconds);
        DetectParams GetDetectParams(UUID scriptID, int detectIndex);
        void SetStartParameter(UUID scriptID, int startParam);
        int GetStartParameter(UUID scriptID);
        void SetScriptMinEventDelay(UUID scriptID, double minDelay);
        void ApiResetScript(UUID scriptID);
        int AddListener(UUID scriptID, UUID hostObjectID, int channel, string name, UUID keyID, string message);
        void RemoveListener(UUID scriptID, int handle);
        void RemoveListeners(UUID scriptID);
        void SetListenerState(UUID scriptID, int handle, bool enabled);
        void SensorOnce(UUID scriptID, UUID hostObjectID, string name, UUID keyID, int type, double range, double arc);
        void SensorRepeat(UUID scriptID, UUID hostObjectID, string name, UUID keyID, int type, double range, double arc, double rate);
        void SensorRemove(UUID scriptID);
    }
}
