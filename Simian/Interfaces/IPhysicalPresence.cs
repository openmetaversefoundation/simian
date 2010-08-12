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
    public enum MovementState
    {
        Walking,
        Running,
        Flying
    }

    public interface IPhysicalPresence : IPhysical
    {
        /// <summary>Requested velocity, given in meters per second</summary>
        Vector3 InputVelocity { get; set; }
        /// <summary>Requested movement state</summary>
        MovementState MovementState { get; set; }
        /// <summary>Ground plane the presence is currently standing on, 
        /// default is Vector4.UnitW</summary>
        Vector4 CollisionPlane { get; set; }
        /// <summary>Environment.TickCount of when a jump started, 0 when no
        /// jump is in progress, and -1 after the presence leaves the ground</summary>
        int JumpStart { get; set; }
        /// <summary>Number of milliseconds this presense will remain stunned (from falling down, etc)</summary>
        int StunMS { get; set; }
        
        /// <summary>Animations currently being played for this avatar</summary>
        AnimationSet Animations { get; }

        /// <summary>Previous movement state</summary>
        MovementState LastMovementState { get; set; }
    }
}
