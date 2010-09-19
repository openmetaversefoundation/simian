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
using System.Threading;
using log4net;
using OpenMetaverse;

namespace Simian.Physics.Simple
{
    [SceneModule("SimplePhysics")]
    public class SimplePhysics : ISceneModule, IPhysicsEngine
    {
        public event EventHandler<EntityCollisionArgs> OnEntityCollision;
        public event EventHandler<EntityRotationAxisArgs> OnEntitySetRotationAxis;
        public event EventHandler<EntityImpulseArgs> OnEntityApplyImpulse;
        public event EventHandler<EntityImpulseArgs> OnEntityApplyRotationalImpulse;
        public event EventHandler<EntityTorqueArgs> OnEntitySetTorque;

        const int TARGET_FRAMES_PER_SECOND = 10;
        const float TARGET_FRAME_TIME = 1.0f / (float)TARGET_FRAMES_PER_SECOND;

        const float COLLISION_MARGIN = 0.3f; //margin for collisions on the x/y planes
        const float GRAVITY = 9.80665f; //meters/sec
        const float FALL_DELAY = 0.33f; //seconds before starting animation
        const float FALL_FORGIVENESS = 0.5f; //fall buffer in meters
        const float JUMP_IMPULSE_VERTICAL = 5f; //boost amount in meters/sec
        const float JUMP_IMPULSE_HORIZONTAL = 2.5f; //boost amount in meters/sec
        const float INITIAL_HOVER_IMPULSE = 2f; //boost amount in meters/sec
        const float PREJUMP_DELAY = 0.25f; //seconds before actually jumping
        const float AVATAR_TERMINAL_VELOCITY = 54f; //~120mph

        const float SQRT_TWO = 1.41421356f;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IScheduler m_scheduler;
        private ITerrain m_terrain;
        private bool m_running = true;
        /// <summary>Tracks physics simulation slowdown compared to our target 
        /// FPS. Value range from 0.0 to 1.0 (one meaning no slowdown)</summary>
        private float m_timeDilation = 1.0f;
        /// <summary>Stores the most recent frame times for physics steps</summary>
        private int[] m_frameTimes = new int[TARGET_FRAMES_PER_SECOND];
        /// <summary>Keeps track of where to store the next frame time</summary>
        private int m_frameTimesIndex;
        private float m_elapsedTime;
        private MapAndArray<uint, IPhysical> m_activePhysicsEntities = new MapAndArray<uint, IPhysical>();

        public float TimeDilation { get { return m_timeDilation; } }
        public float FPS { get { return m_timeDilation * (float)TARGET_FRAMES_PER_SECOND; } }
        public float FrameTimeMS
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < m_frameTimes.Length; i++)
                    sum += m_frameTimes[i];
                return (float)sum / (float)m_frameTimes.Length;
            }
        }

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_scheduler = m_scene.Simian.GetAppModule<IScheduler>();
            if (m_scheduler == null)
            {
                m_log.Error("Can't load SimplePhysics without an IScheduler");
                return;
            }

            m_terrain = m_scene.GetSceneModule<ITerrain>();

            m_scene.OnEntityAddOrUpdate += EntityAddOrUpdateHandler;
            m_scene.OnEntityRemove += EntityRemoveHandler;

            m_scheduler.StartThread(PhysicsLoop, "SimplePhysics", ThreadPriority.Normal, true);
        }

        public void Stop()
        {
            m_running = false;

            m_scene.OnEntityAddOrUpdate -= EntityAddOrUpdateHandler;
            m_scene.OnEntityRemove -= EntityRemoveHandler;
        }

        /// <summary>
        /// Change the axis of rotation for an entity
        /// </summary>
        /// <param name="sender">Calling object</param>
        /// <param name="entity">Entity to change the axis of rotation for</param>
        /// <param name="rotationAxis">New axis of rotation</param>
        public void EntitySetRotationAxis(object sender, IPhysical entity, Vector3 rotationAxis)
        {
            EventHandler<EntityRotationAxisArgs> callback = OnEntitySetRotationAxis;
            if (callback != null)
                callback(sender, new EntityRotationAxisArgs { Entity = entity, RotationAxis = rotationAxis });

            // TODO: Implement this
        }

        /// <summary>
        /// Applies a physical force to an entity at its center of mass
        /// </summary>
        /// <param name="sender">Calling object</param>
        /// <param name="entity">Entity to apply a force to</param>
        /// <param name="impulse">Force vector to apply to the entity at its 
        /// center of mass</param>
        public void EntityApplyImpulse(object sender, IPhysical entity, Vector3 impulse)
        {
            EventHandler<EntityImpulseArgs> callback = OnEntityApplyImpulse;
            if (callback != null)
                callback(sender, new EntityImpulseArgs { Entity = entity, Impulse = impulse });

            // TODO: Implement this
        }

        /// <summary>
        /// Apply a physical angular force to an entity
        /// </summary>
        /// <param name="sender">Calling object</param>
        /// <param name="entity">Entity to apply the angular force to</param>
        /// <param name="impulse">Angular force to apply to the entity</param>
        public void EntityApplyRotationalImpulse(object sender, IPhysical entity, Vector3 impulse)
        {
            EventHandler<EntityImpulseArgs> callback = OnEntityApplyRotationalImpulse;
            if (callback != null)
                callback(sender, new EntityImpulseArgs { Entity = entity, Impulse = impulse });

            // TODO: Implement this
        }

        /// <summary>
        /// Set a constant angular velocity on an entity
        /// </summary>
        /// <param name="sender">Calling object</param>
        /// <param name="entity">Entity to set the angular velocity for</param>
        /// <param name="torque">Angular velocity to set on the entity</param>
        public void EntitySetTorque(object sender, IPhysical entity, Vector3 torque)
        {
            EventHandler<EntityTorqueArgs> callback = OnEntitySetTorque;
            if (callback != null)
                callback(sender, new EntityTorqueArgs { Entity = entity, Torque = torque });

            // TODO: Implement this
        }

        public bool FullSceneCollisionTest(bool includeTerrain, Ray ray, ISceneEntity castingObj, out ISceneEntity collisionObj, out float dist)
        {
            ISceneEntity minCollider = null;
            float minDist = Single.MaxValue;

            // Test against the AABB of every entity in the scene (can be slow!)
            m_scene.ForEachEntity(
                delegate(ISceneEntity entity)
                {
                    if (entity == castingObj)
                        return;

                    float thisDist;
                    if (entity is IPhysical && EntityCollisionTest(ray, (IPhysical)entity, out thisDist) && thisDist < minDist)
                    {
                        minDist = thisDist;
                        minCollider = entity;
                    }
                }
            );

            collisionObj = minCollider;
            dist = minDist;

            if (includeTerrain)
            {
                // Test against the terrain
                float terrainDist;
                if (RayHeightmap.CollisionTest(ray, m_terrain.GetHeightmap(), 256, 256, 256.0f, out terrainDist) && terrainDist < dist)
                {
                    dist = terrainDist;
                    collisionObj = null;
                    return true;
                }
            }

            return collisionObj != null;
        }

        public bool EntityCollisionTest(Ray ray, IPhysical obj, out float distance)
        {
            float unused;
            return RayAABB.CollisionTestSmits(obj.SceneAABB, ray, out distance, out unused);
        }

        private void EntityAddOrUpdateHandler(object sender, EntityAddOrUpdateArgs e)
        {
            // Add all entities with dynamics enabled to our dictionary
            if (e.UpdateFlags.HasFlag(UpdateFlags.PhysicalStatus) && !(e.Entity is IScenePresence) && e.Entity is IPhysical)
            {
                IPhysical physical = (IPhysical)e.Entity;

                if (physical.DynamicsEnabled)
                    m_activePhysicsEntities.Add(physical.LocalID, physical);
                else
                    m_activePhysicsEntities.Remove(physical.LocalID);
            }
        }

        private void EntityRemoveHandler(object sender, EntityArgs e)
        {
            // Remove all entities with dynamics enabled from our dictionary
            if (e.Entity is IPhysical)
            {
                IPhysical physical = (IPhysical)e.Entity;

                if (!physical.DynamicsEnabled)
                    m_activePhysicsEntities.Remove(physical.LocalID);
            }
        }

        #region Physics Update

        private void PhysicsLoop()
        {
            m_elapsedTime = 0f;
            int sleepMS;

            while (m_running)
            {
                DateTime start = DateTime.UtcNow;
                
                // Update the avatars
                m_scene.ForEachPresence(UpdateEntity);

                // Update the entities with dynamics enabled
                IPhysical[] physicals = m_activePhysicsEntities.GetArray();
                for (int i = 0; i < physicals.Length; i++)
                    UpdateEntity(physicals[i]);

                // Measure the duration of this frame
                m_elapsedTime = (float)(DateTime.UtcNow - start).TotalSeconds;
                m_frameTimes[m_frameTimesIndex++] = (int)(m_elapsedTime * 1000.0f);
                if (m_frameTimesIndex >= m_frameTimes.Length)
                    m_frameTimesIndex = 0;

                // Calculate time dilation and decide if we need to sleep to limit FPS
                if (m_elapsedTime < TARGET_FRAME_TIME)
                {
                    m_timeDilation = 1f;
                    sleepMS = (int)((TARGET_FRAME_TIME - m_elapsedTime) * 1000f);
                    Thread.Sleep(sleepMS);
                    m_elapsedTime = TARGET_FRAME_TIME;
                }
                else
                {
                    m_timeDilation = (1f / m_elapsedTime) / (float)TARGET_FRAMES_PER_SECOND;
                }

                m_scheduler.ThreadKeepAlive();
            }
        }

        private void UpdateEntity(ISceneEntity entity)
        {
            if (!(entity is IPhysical)) return;
            if (entity is IScenePresence && ((IScenePresence)entity).IsChildPresence) return;

            float elapsedTime = m_elapsedTime;
            if (elapsedTime <= 0f) return;

            IPhysical physical = (IPhysical)entity;
            if (!physical.DynamicsEnabled) return;

            IPhysicalPresence presence = (physical is IPhysicalPresence) ? (IPhysicalPresence)physical : null;
            Vector3 velocity = physical.Velocity * elapsedTime;
            Vector3 position = physical.RelativePosition;

            Vector3 move = (presence != null) ? presence.InputVelocity : Vector3.Zero;
            bool jumping = (presence != null) ? presence.JumpStart != 0 : false;
            float gravity = 0f;
            float waterHeight = 0.0f;
            if (m_terrain != null)
                waterHeight = m_terrain.WaterHeight;
            float waterChestHeight = waterHeight - (physical.Scale.Z * .33f);
            float speed = elapsedTime;
            float fallElapsed = (float)(Util.TickCount() - physical.FallStart) / 1000f;
            UUID anim = UUID.Zero;
            List<ISceneEntity> colliders = new List<ISceneEntity>();

            if (presence != null && presence.StunMS > 0)
            {
                move = Vector3.Zero;
                presence.StunMS -= (int)((float)1000 * elapsedTime);
            }

            ISceneEntity collider;
            float collisionDist;

            //if (presence != null && presence.InputDirection != Vector3.Zero)
            //{
            //    // Raycast in the direction the avatar wishes to move
            //    Ray avRay = new Ray(physical.ScenePosition, Vector3.Normalize(presence.InputDirection));
            //    if (FullSceneCollisionTest(false, avRay, presence, out collider, out collisionDist))
            //    {
            //        speed = Math.Min(speed, collisionDist - COLLISION_MARGIN);
            //        m_log.Debug("Raycasted to " + collider.Name + " (" + collider.LocalID + ")");
            //    }
            //}
            //HACK: detect both X and Y ray collisions, since we are not calculating a collision normal
            Vector3 normVel = Vector3.Normalize(velocity);
            if ((normVel.X != 0f || move.X != 0f) && FullSceneCollisionTest(false, new Ray(position, normVel.X > 0 || move.X > 0 ? Vector3.UnitX : -Vector3.UnitX), physical, out collider, out collisionDist) && collisionDist <= COLLISION_MARGIN)
            {
                move.X = 0f;
                velocity.X = 0f;

                if (!colliders.Contains(collider))
                    colliders.Add(collider);
            }
            if ((normVel.Y != 0f || move.Y != 0f) && FullSceneCollisionTest(false, new Ray(position, normVel.Y > 0 || move.Y > 0 ? Vector3.UnitY : -Vector3.UnitY), physical, out collider, out collisionDist) && collisionDist <= COLLISION_MARGIN)
            {
                move.Y = 0f;
                velocity.Y = 0f;

                if (!colliders.Contains(collider))
                    colliders.Add(collider);
            }

            #region Terrain Movement

            float oldFloor = 0.0f, newFloor = 0.0f;
            if (m_terrain != null)
                oldFloor = m_terrain.GetTerrainHeightAt(position.X, position.Y);

            position += (move * speed);

            if (m_terrain != null)
                newFloor = m_terrain.GetTerrainHeightAt(position.X, position.Y);

            if (presence != null)
            {
                if (presence.MovementState != MovementState.Flying && newFloor != oldFloor)
                    speed /= (1f + (SQRT_TWO * Math.Abs(newFloor - oldFloor)));
            }

            //HACK: distance from avatar center to the bottom of its feet
            float distanceFromFloor = physical.Scale.Z * .5f;

            // Raycast for gravity
            Ray ray = new Ray(position, -Vector3.UnitZ);
            if (FullSceneCollisionTest(false, ray, physical, out collider, out collisionDist))
            {
                if (position.Z - collisionDist > newFloor)
                {
                    newFloor = position.Z - collisionDist;

                    //FIXME
                    //if (!colliders.Contains(collider))
                        //colliders.Add(collider);
                }
            }

            float lowerLimit = newFloor + distanceFromFloor;

            #endregion Terrain Movement

            if (presence != null && presence.MovementState == MovementState.Flying)
            {
                #region Flying

                physical.FallStart = 0;
                presence.JumpStart = 0;

                //velocity falloff while flying
                velocity.X *= 0.66f;
                velocity.Y *= 0.66f;
                velocity.Z *= 0.33f;

                if (position.Z == lowerLimit)
                    velocity.Z += INITIAL_HOVER_IMPULSE;

                if (move.X != 0f || move.Y != 0f)
                    anim = Animations.FLY;
                else if (move.Z > 0f)
                    anim = Animations.HOVER_UP;
                else if (move.Z < 0f)
                    anim = Animations.HOVER_DOWN;
                else
                    anim = Animations.HOVER;

                #endregion Flying
            }
            else if (position.Z > lowerLimit + FALL_FORGIVENESS || position.Z <= waterChestHeight)
            {
                #region Falling/Floating/Landing

                if (position.Z > waterHeight)
                { //above water

                    //override controls while drifting
                    move = Vector3.Zero;

                    //keep most of our horizontal inertia
                    velocity.X *= 0.975f;
                    velocity.Y *= 0.975f;

                    if (physical.FallStart == 0) //|| (fallElapsed > FALL_DELAY && velocity.Z >= 0f))
                    { //just started falling
                        physical.FallStart = Util.TickCount();
                    }
                    else
                    {
                        gravity = GRAVITY * fallElapsed * elapsedTime; //normal gravity

                        if (!jumping)
                        { //falling
                            if (fallElapsed > FALL_DELAY)
                            { //falling long enough to trigger the animation
                                anim = Animations.FALLDOWN;
                            }
                        }
                    }
                }
                else if (position.Z >= waterChestHeight)
                { //at the water line

                    velocity *= 0.1f;
                    velocity.Z = 0f;
                    physical.FallStart = 0;

                    if (move.Z < 1f)
                        position.Z = waterChestHeight;

                    if (move.Z > 0f)
                        anim = Animations.HOVER_UP;
                    else if (move.X != 0f || move.Y != 0f)
                        anim = Animations.SWIM_FORWARD;
                    else
                        anim = Animations.HOVER;
                }
                else
                { //underwater

                    velocity *= 0.1f;
                    velocity.Z += 0.75f * elapsedTime; // buoyant

                    if (move.Z < 0f)
                        anim = Animations.SWIM_DOWN;
                    else
                        anim = Animations.SWIM_FORWARD;
                }

                #endregion Falling/Floating/Landing
            }
            else
            {
                #region Ground Movement

                if (presence != null)
                {
                    if (presence.JumpStart == 0 && physical.FallStart > 0)
                    {
                        if (fallElapsed >= FALL_DELAY * 4)
                        {
                            anim = Animations.STANDUP;
                            presence.StunMS = 2000;
                        }
                        else if (fallElapsed >= FALL_DELAY * 3)
                        {
                            anim = Animations.MEDIUM_LAND;
                            presence.StunMS = 1000;
                        }
                        else if (fallElapsed >= FALL_DELAY * 2)
                        {
                            anim = Animations.LAND;
                            presence.StunMS = 500;
                        }
                        if (presence.Animations.SetDefaultAnimation(anim, 1))
                            m_scene.SendPresenceAnimations(this, presence);
                    }
                }

                physical.FallStart = 0;

                //friction
                velocity *= 0.2f;
                velocity.Z = 0f;
                position.Z = lowerLimit;

                if (presence != null)
                {
                    if (move.Z > 0f)
                    { //jumping
                        if (!jumping)
                        { //begin prejump
                            move.Z = 0f; //override Z control
                            anim = Animations.PRE_JUMP;

                            presence.JumpStart = Util.TickCount();
                        }
                        else if (Util.TickCount() - presence.JumpStart > PREJUMP_DELAY * 1000)
                        { //start actual jump

                            if (presence.JumpStart == -1)
                            {
                                //already jumping! end current jump
                                presence.JumpStart = 0;
                                return;
                            }

                            anim = Animations.JUMP;

                            Vector3 normalVel = Vector3.Normalize(velocity);

                            velocity.X += normalVel.X * JUMP_IMPULSE_HORIZONTAL * elapsedTime;
                            velocity.Y += normalVel.Y * JUMP_IMPULSE_HORIZONTAL * elapsedTime;
                            velocity.Z = JUMP_IMPULSE_VERTICAL * elapsedTime;

                            presence.JumpStart = -1; //flag that we are currently jumping
                        }
                        else move.Z = 0; //override Z control
                    }
                    else
                    { //not jumping
                        presence.JumpStart = 0;

                        if (move.X != 0 || move.Y != 0)
                        {
                            if (move.Z < 0)
                                anim = Animations.CROUCHWALK;
                            else if (presence.MovementState == MovementState.Running)
                                anim = Animations.RUN;
                            else
                                anim = Animations.WALK;
                        }
                        else
                        {
                            if (move.Z < 0)
                                anim = Animations.CROUCH;
                            else
                                anim = Animations.STAND;
                        }
                    }
                }

                #endregion Ground Movement
            }

            if (presence != null)
            {
                if (anim != UUID.Zero && presence.StunMS <= 0 && presence.Animations.SetDefaultAnimation(anim, 1))
                    m_scene.SendPresenceAnimations(this, presence);
            }

            float maxVel = AVATAR_TERMINAL_VELOCITY * elapsedTime;

            #region Update Physical State

            // Calculate how far we moved this frame
            Vector3 moved = position - physical.RelativePosition;
            if (moved.Z < -maxVel)
                moved.Z = -maxVel;
            else if (moved.Z > maxVel)
                moved.Z = maxVel;

            position += velocity;

            moved.Z = moved.Z - gravity;
            velocity += moved;
            if (velocity.Z < -maxVel)
                velocity.Z = -maxVel;
            else if (velocity.Z > maxVel)
                velocity.Z = maxVel;

            if (position.Z < lowerLimit)
                position.Z = lowerLimit;
            if (position.Z < lowerLimit + COLLISION_MARGIN)
                velocity.Z = 0f;

            physical.Velocity = velocity / elapsedTime;
            physical.RelativePosition = position;

            #endregion Update Physical State

            EventHandler<EntityCollisionArgs> callback = OnEntityCollision;
            if (callback != null)
            {
                for (int i = 0, len = colliders.Count; i < len; i++)
                {
                    callback(this, new EntityCollisionArgs { First = physical, Second = colliders[i] });
                    callback(this, new EntityCollisionArgs { First = colliders[i], Second = physical });
                }
            }

            m_scene.EntityAddOrUpdate(this, physical, UpdateFlags.Position | UpdateFlags.Velocity, 0);
        }

        #endregion Physics Update
    }
}