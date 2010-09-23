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
    public interface IPhysical : ILinkable
    {
        Vector3 Velocity { get; set; }
        Vector3 Acceleration { get; set; }
        Vector3 AngularVelocity { get; set; }
        Vector3 RotationAxis { get; set; }

        bool CollisionsEnabled { get; set; }
        bool DynamicsEnabled { get; set; }
        bool Frozen { get; set; }

        Vector3 LastVelocity { get; set; }
        Vector3 LastAcceleration { get; set; }
        Vector3 LastAngularVelocity { get; set; }

        /// <summary>Number of milliseconds this presence has been falling</summary>
        int FallStart { get; set; }

        /// <summary>
        /// Computes the mass (volume times density) of this entity, taking 
        /// scale into account
        /// </summary>
        /// <returns>The entity's current mass</returns>
        float GetMass();

        /// <summary>
        /// Signals a change in the entities volume or density. Used to clear
        /// any cached values
        /// </summary>
        void ResetMass();

        /// <summary>
        /// Gets the type of physical representation this entity uses for 
        /// physics simulation
        /// </summary>
        /// <returns>This entities physical proxy type</returns>
        PhysicsType GetPhysicsType();

        /// <summary>
        /// Calculates a unique identifier for this entities physical mesh or 
        /// convex hull. If a mesh or convex hull is not used, 0 is returned
        /// </summary>
        /// <returns>A 64-bit unique identifier for this entities physical mesh
        /// or convex hull, or 0 if a mesh or convex hull is not used</returns>
        ulong GetPhysicsKey();

        /// <summary>
        /// Creates a basic mesh representation for this entity
        /// </summary>
        /// <returns>A BasicMesh for this entity</returns>
        BasicMesh GetBasicMesh();

        /// <summary>
        /// Creates a series of convex hulls representing this entity
        /// </summary>
        /// <returns>A ConvexHull for this entity</returns>
        ConvexHullSet GetConvexHulls();
    }
}
