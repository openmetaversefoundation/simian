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
    /// <summary>
    /// Static pre-defined animations available to all agents
    /// </summary>
    public static class Animations
    {
        /// <summary>Agent hunched over (away)</summary>
        public readonly static UUID AWAY = new UUID("fd037134-85d4-f241-72c6-4f42164fedee");
        /// <summary>Agent doing a backflip</summary>
        /// <summary>Agent in busy mode</summary>
        public readonly static UUID BUSY = new UUID("efcf670c-2d18-8128-973a-034ebc806b67");
        /// <summary>Agent crouching</summary>
        public readonly static UUID CROUCH = new UUID("201f3fdf-cb1f-dbec-201f-7333e328ae7c");
        /// <summary>Agent crouching while walking</summary>
        public readonly static UUID CROUCHWALK = new UUID("47f5f6fb-22e5-ae44-f871-73aaaf4a6022");
        /// <summary>Agent unanimated with arms out (e.g. setting appearance)</summary>
        public readonly static UUID CUSTOMIZE = new UUID("038fcec9-5ebd-8a8e-0e2e-6e71a0a1ac53");
        /// <summary>Agent re-animated after set appearance finished</summary>
        public readonly static UUID CUSTOMIZE_DONE = new UUID("6883a61a-b27b-5914-a61e-dda118a9ee2c");
        /// <summary>Agent on ground unanimated</summary>
        public readonly static UUID DEAD = new UUID("57abaae6-1d17-7b1b-5f98-6d11a6411276");
        public readonly static UUID FALLDOWN = new UUID("666307d9-a860-572d-6fd4-c3ab8865c094");
        /// <summary>Agent walking (feminine version)</summary>
        public readonly static UUID FEMALE_WALK = new UUID("f5fc7433-043d-e819-8298-f519a119b688");
        /// <summary>Agent in superman position</summary>
        public readonly static UUID FLY = new UUID("aec4610c-757f-bc4e-c092-c6e9caf18daf");
        /// <summary>Agent in superman position</summary>
        public readonly static UUID FLYSLOW = new UUID("2b5a38b2-5e00-3a97-a495-4c826bc443e6");
        /// <summary>Agent in static hover</summary>
        public readonly static UUID HOVER = new UUID("4ae8016b-31b9-03bb-c401-b1ea941db41d");
        /// <summary>Agent hovering downward</summary>
        public readonly static UUID HOVER_DOWN = new UUID("20f063ea-8306-2562-0b07-5c853b37b31e");
        /// <summary>Agent hovering upward</summary>
        public readonly static UUID HOVER_UP = new UUID("62c5de58-cb33-5743-3d07-9e4cd4352864");
        /// <summary>Agent hopping</summary>
        public readonly static UUID HOP = new UUID("abe0aeb0-de97-4d29-8a5b-2847c8e2f1cf");
        /// <summary>Agent jumping</summary>
        public readonly static UUID JUMP = new UUID("2305bd75-1ca9-b03b-1faa-b176b8a8c49e");
        /// <summary>Agent landing from jump, finished flight, etc</summary>
        public readonly static UUID LAND = new UUID("7a17b059-12b2-41b1-570a-186368b6aa6f");
        /// <summary>Agent landing from jump, finished flight, etc</summary>
        public readonly static UUID MEDIUM_LAND = new UUID("f4f00d6e-b9fe-9292-f4cb-0ae06ea58d57");
        /// <summary>Agent preparing for jump (bending knees)</summary>
        public readonly static UUID PRE_JUMP = new UUID("7a4e87fe-de39-6fcb-6223-024b00893244");
        /// <summary>Agent running</summary>
        public readonly static UUID RUN = new UUID("05ddbff8-aaa9-92a1-2b74-8fe77a29b445");
        /// <summary>Agent in sit position</summary>
        public readonly static UUID SIT = new UUID("1a5fe8ac-a804-8a5d-7cbd-56bd83184568");
        /// <summary>Agent in sit position (feminine)</summary>
        public readonly static UUID SIT_FEMALE = new UUID("b1709c8d-ecd3-54a1-4f28-d55ac0840782");
        /// <summary>Agent in sit position (generic)</summary>
        public readonly static UUID SIT_GENERIC = new UUID("245f3c54-f1c0-bf2e-811f-46d8eeb386e7");
        /// <summary>Agent sitting on ground</summary>
        public readonly static UUID SIT_GROUND = new UUID("1c7600d6-661f-b87b-efe2-d7421eb93c86");
        /// <summary>Agent standing up from a sitting position</summary>
        public readonly static UUID SIT_TO_STAND = new UUID("a8dee56f-2eae-9e7a-05a2-6fb92b97e21e");
        /// <summary>Agent standing</summary>
        public readonly static UUID STAND = new UUID("2408fe9e-df1d-1d7d-f4ff-1384fa7b350f");
        /// <summary>Agent standing up</summary>
        public readonly static UUID STANDUP = new UUID("3da1d753-028a-5446-24f3-9c9b856d9422");
        /// <summary>Agent turning to the left</summary>
        public readonly static UUID TURNLEFT = new UUID("56e0ba0d-4a9f-7f27-6117-32f2ebbf6135");
        /// <summary>Agent turning to the right</summary>
        public readonly static UUID TURNRIGHT = new UUID("2d6daa51-3192-6794-8e2e-a15f8338ec30");
        /// <summary>Agent typing</summary>
        public readonly static UUID TYPE = new UUID("c541c47f-e0c0-058b-ad1a-d6ae3a4584d9");
        /// <summary>Agent walking</summary>
        public readonly static UUID WALK = new UUID("6ed24bd8-91aa-4b12-ccc7-c97c857ab4e0");

        //Custom animations contributed for simian

        /// <summary>Agent swimming</summary>
        public readonly static UUID SWIM_FORWARD = new UUID("6979fef2-d106-7b25-8e24-d2de06cedabd");
        /// <summary>Agent swimming down</summary>
        public readonly static UUID SWIM_DOWN = new UUID("ee6abf91-4fdb-3ee1-bd06-99ec8234ced7");
        
    }
}
