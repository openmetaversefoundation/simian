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
using OpenMetaverse.Packets;

namespace Simian.Protocols.Linden.Packets
{
    [SceneModule("Movement")]
    public class Movement : ISceneModule
    {
        const float WALK_SPEED = 3f;
        const float RUN_SPEED = 5f;
        const float FLY_SPEED = 10f;
        const float SQRT_TWO = 1.41421356f;

        // Scripting event flags
        const int CHANGED_LINK = 32;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);
        
        private int m_coarseLocationSeconds;
        private IScene m_scene;
        private LLUDP m_udp;
        private ILSLScriptEngine m_lslScriptEngine;

        public void Start(IScene scene)
        {
            m_scene = scene;
            m_lslScriptEngine = m_scene.GetSceneModule<ILSLScriptEngine>();

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.AgentUpdate, AgentUpdateHandler);
                m_udp.AddPacketHandler(PacketType.SetAlwaysRun, SetAlwaysRunHandler);
                m_udp.AddPacketHandler(PacketType.AgentRequestSit, AgentRequestSitHandler);
                m_udp.AddPacketHandler(PacketType.AgentSit, AgentSitHandler);

                m_scene.Simian.AddHeartbeatHandler(SendCoarseLocations);
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.AgentUpdate, AgentUpdateHandler);
                m_udp.RemovePacketHandler(PacketType.SetAlwaysRun, SetAlwaysRunHandler);
                m_udp.RemovePacketHandler(PacketType.AgentRequestSit, AgentRequestSitHandler);
                m_udp.RemovePacketHandler(PacketType.AgentSit, AgentSitHandler);

                m_scene.Simian.RemoveHeartbeatHandler(SendCoarseLocations);
            }
        }

        private void AgentUpdateHandler(Packet packet, LLAgent agent)
        {
            AgentUpdatePacket update = (AgentUpdatePacket)packet;

            // Update rotation if the agent is not sitting on anything
            if (agent.Parent == null)
                agent.RelativeRotation = update.AgentData.BodyRotation;

            agent.CameraPosition = update.AgentData.CameraCenter;
            agent.CameraAtAxis = update.AgentData.CameraAtAxis;
            agent.CameraLeftAxis = update.AgentData.CameraLeftAxis;
            agent.CameraUpAxis = update.AgentData.CameraUpAxis;
            agent.DrawDistance = update.AgentData.Far;

            agent.ControlFlags = (AgentManager.ControlFlags)update.AgentData.ControlFlags;
            agent.State = (AgentState)update.AgentData.State;
            agent.HideTitle = update.AgentData.Flags != 0;

            #region Standing

            ILinkable parent = agent.Parent;
            if (parent != null && (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_STAND_UP) == AgentManager.ControlFlags.AGENT_CONTROL_STAND_UP)
            {
                agent.SetParent(null, true, true);

                agent.RelativePosition = parent.ScenePosition
                    + Vector3.Transform(agent.SitPosition, Matrix4.CreateFromQuaternion(agent.SitRotation))
                    + Vector3.UnitZ;

                agent.Animations.ResetDefaultAnimation();
                m_scene.SendPresenceAnimations(this, agent);

                agent.CollisionsEnabled = true;
                agent.DynamicsEnabled = true;

                m_scene.EntityAddOrUpdate(this, agent, UpdateFlags.Position | UpdateFlags.Rotation, (uint)LLUpdateFlags.PrimFlags);
                return;
            }

            #endregion Standing

            #region Inputs

            // Create forward and left vectors from the current avatar rotation
            Matrix4 rotMatrix = Matrix4.CreateFromQuaternion(agent.RelativeRotation);
            Vector3 fwd = Vector3.Transform(Vector3.UnitX, rotMatrix);
            Vector3 left = Vector3.Transform(Vector3.UnitY, rotMatrix);

            // Check control flags
            bool heldForward = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_AT_POS) == AgentManager.ControlFlags.AGENT_CONTROL_AT_POS;
            bool heldBack = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG) == AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG;
            bool heldLeft = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS) == AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS;
            bool heldRight = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG) == AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG;
            //bool heldTurnLeft = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_TURN_LEFT) == AgentManager.ControlFlags.AGENT_CONTROL_TURN_LEFT;
            //bool heldTurnRight = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_TURN_RIGHT) == AgentManager.ControlFlags.AGENT_CONTROL_TURN_RIGHT;
            bool heldUp = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_UP_POS) == AgentManager.ControlFlags.AGENT_CONTROL_UP_POS;
            bool heldDown = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG) == AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG;
            bool flying = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_FLY) == AgentManager.ControlFlags.AGENT_CONTROL_FLY;
            //bool mouselook = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK) == AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK;
            bool nudgeForward = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_POS) == AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_POS;
            bool nudgeBack = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_NEG) == AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_NEG;
            bool nudgeLeft = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_POS) == AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_POS;
            bool nudgeRight = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_NEG) == AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_NEG;
            bool nudgeUp = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_POS) == AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_POS;
            bool nudgeDown = (agent.ControlFlags & AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_NEG) == AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_NEG;

            // direction in which the avatar is trying to move
            Vector3 move = Vector3.Zero;
            if (heldForward) { move.X += fwd.X; move.Y += fwd.Y; }
            else if (nudgeForward) { move.X += fwd.X * 0.5f; move.Y += fwd.Y * 0.5f; }
            if (heldBack) { move.X -= fwd.X; move.Y -= fwd.Y; }
            else if (nudgeBack) { move.X -= fwd.X * 0.5f; move.Y -= fwd.Y * 0.5f; }

            if (heldLeft) { move.X += left.X; move.Y += left.Y; }
            else if (nudgeLeft) { move.X += left.X * 0.5f; move.Y += left.Y * 0.5f; }
            if (heldRight) { move.X -= left.X; move.Y -= left.Y; }
            else if (nudgeRight) { move.X -= left.X * 0.5f; move.Y -= left.Y * 0.5f; }

            if (heldUp) { move.Z += 1f; }
            else if (nudgeUp) { move.Z += 0.5f; }
            if (heldDown) { move.Z -= 1f; }
            else if (nudgeDown) { move.Z -= 0.5f; }

            bool jumping = agent.JumpStart != 0;

            float speed = (flying ? FLY_SPEED : agent.IsRunning && !jumping ? RUN_SPEED : WALK_SPEED);
            if ((heldForward || heldBack) && (heldLeft || heldRight))
                speed /= SQRT_TWO;

            #endregion Inputs

            agent.InputVelocity = move * speed;
            agent.LastMovementState = agent.MovementState;
            agent.MovementState = (flying ? MovementState.Flying : agent.IsRunning && !jumping ? MovementState.Running : MovementState.Walking);
        }

        private void SetAlwaysRunHandler(Packet packet, LLAgent agent)
        {
            SetAlwaysRunPacket run = (SetAlwaysRunPacket)packet;
            agent.IsRunning = run.AgentData.AlwaysRun;
        }

        private void AgentRequestSitHandler(Packet packet, LLAgent agent)
        {
            AgentRequestSitPacket request = (AgentRequestSitPacket)packet;
            agent.RequestedSitTarget = request.TargetObject.TargetID;
            agent.RequestedSitOffset = request.TargetObject.Offset;

            //TODO: move to AgentSitHandler when we figure out how to make the client send AgentSit
            ISceneEntity seat;
            if (m_scene.TryGetEntity(agent.RequestedSitTarget, out seat) && seat is ILinkable)
            {
                agent.SetParent((ILinkable)seat, false, false);

                AvatarSitResponsePacket response = new AvatarSitResponsePacket();
                response.SitObject.ID = agent.RequestedSitTarget;

                if (seat is LLPrimitive)
                {
                    LLPrimitive prim = (LLPrimitive)seat;
                    if (prim.SitPosition != Vector3.Zero)
                    {
                        // llSitTarget is set

                        response.SitTransform.SitPosition = prim.SitPosition;
                        response.SitTransform.SitRotation = prim.SitRotation;

                        agent.RelativePosition = LLUtil.GetSitTarget(prim.SitPosition, agent.Scale);
                        agent.RelativeRotation = prim.SitRotation;
                    }
                    else
                    {
                        // No sit target set

                        Vector3 sitPos = LLUtil.GetSitTarget(agent.RequestedSitOffset, agent.Scale);

                        response.SitTransform.SitPosition = sitPos;
                        response.SitTransform.SitRotation = Quaternion.Identity;

                        agent.RelativePosition = sitPos;
                        agent.RelativeRotation = Quaternion.Identity;
                    }
                }

                m_udp.SendPacket(agent, response, ThrottleCategory.Task, false);

                m_scene.EntityAddOrUpdate(this, agent, UpdateFlags.Parent, 0);

                agent.Animations.SetDefaultAnimation(OpenMetaverse.Animations.SIT, 1);
                m_scene.SendPresenceAnimations(this, agent);

                if (m_lslScriptEngine != null)
                    m_lslScriptEngine.PostObjectEvent(seat.ID, "changed", new object[] { CHANGED_LINK }, new DetectParams[0]);
            }
            else
            {
                //TODO: send error message
            }
        }

        private void AgentSitHandler(Packet packet, LLAgent agent)
        {
            AgentSitPacket sit = (AgentSitPacket)packet;
        }

        private void SendCoarseLocations(object sender, System.Timers.ElapsedEventArgs e)
        {
            const int MAX_LOCATIONS = 60;
            const int INTERVAL_SECONDS = 3;

            if (++m_coarseLocationSeconds >= INTERVAL_SECONDS)
            {
                m_coarseLocationSeconds = 0;

                IScenePresence[] presences = m_scene.GetPresences();
                if (presences.Length == 0)
                    return;

                // Prune out child agents
                List<IScenePresence> rootPresences = new List<IScenePresence>(presences.Length);
                for (int i = 0; i < presences.Length; i++)
                {
                    if (!presences[i].IsChildPresence)
                        rootPresences.Add(presences[i]);
                }

                // Clamp the maximum locations to put in this packet
                int count = Math.Min(rootPresences.Count, MAX_LOCATIONS);
                if (count == 0)
                    return;

                // Create the location and agentID blocks
                CoarseLocationUpdatePacket.AgentDataBlock[] uuids = new CoarseLocationUpdatePacket.AgentDataBlock[count];
                CoarseLocationUpdatePacket.LocationBlock[] locations = new CoarseLocationUpdatePacket.LocationBlock[count];
                for (int i = 0; i < count; i++)
                {
                    IScenePresence presence = rootPresences[i];
                    Vector3 pos = presence.ScenePosition;

                    uuids[i] = new CoarseLocationUpdatePacket.AgentDataBlock { AgentID = presence.ID };
                    locations[i] = new CoarseLocationUpdatePacket.LocationBlock { X = (byte)pos.X, Y = (byte)pos.Y, Z = (byte)pos.Z };
                }

                for (int i = 0; i < rootPresences.Count; i++)
                {
                    if (rootPresences[i] is LLAgent)
                    {
                        LLAgent agent = (LLAgent)rootPresences[i];

                        CoarseLocationUpdatePacket packet = new CoarseLocationUpdatePacket();
                        packet.Header.Reliable = false;
                        packet.AgentData = uuids;
                        packet.Location = locations;

                        // Only the first MAX_COUNT avatars are in the list
                        int you = (i < count) ? i : -1;
                        packet.Index.You = (short)you;

                        // TODO: Support Prey
                        packet.Index.Prey = -1;

                        m_udp.SendPacket(agent, packet, ThrottleCategory.Task, false);
                    }
                }
            }
        }
    }
}
