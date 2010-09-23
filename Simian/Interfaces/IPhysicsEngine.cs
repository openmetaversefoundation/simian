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

namespace Simian
{
    public class EntityCollisionArgs : EventArgs
    {
        public ISceneEntity First;
        public ISceneEntity Second;
    }

    public class EntityRotationAxisArgs : EntityArgs
    {
        public Vector3 RotationAxis;
    }

    public class EntityImpulseArgs : EntityArgs
    {
        public Vector3 Impulse;
    }

    public class EntityTorqueArgs : EntityArgs
    {
        public Vector3 Torque;
    }

    public interface IPhysicsEngine
    {
        event EventHandler<EntityCollisionArgs> OnEntityCollision;
        event EventHandler<EntityRotationAxisArgs> OnEntitySetRotationAxis;
        event EventHandler<EntityImpulseArgs> OnEntityApplyImpulse;
        event EventHandler<EntityImpulseArgs> OnEntityApplyRotationalImpulse;
        event EventHandler<EntityTorqueArgs> OnEntitySetTorque;

        float TimeDilation { get; }
        float FPS { get; }
        float FrameTimeMS { get; }

        void EntitySetRotationAxis(object sender, IPhysical entity, Vector3 rotationAxis);
        void EntityApplyImpulse(object sender, IPhysical entity, Vector3 impulse);
        void EntityApplyRotationalImpulse(object sender, IPhysical entity, Vector3 impulse);
        void EntitySetTorque(object sender, IPhysical entity, Vector3 torque);
        bool EntityCollisionTest(Ray ray, IPhysical obj, out float distance);
        bool FullSceneCollisionTest(bool includeTerrain, Ray ray, ISceneEntity castingObj, out ISceneEntity collisionObj, out float distance);
    }
}
