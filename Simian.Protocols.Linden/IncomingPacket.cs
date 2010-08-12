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
using OpenMetaverse.Packets;

namespace Simian.Protocols.Linden
{
    /// <summary>
    /// Holds a reference to a <seealso cref="LLUDPClient"/> and a <seealso cref="Packet"/>
    /// for incoming packets
    /// </summary>
    public sealed class IncomingPacket
    {
        /// <summary>Client this packet came from</summary>
        public LLAgent Agent;
        /// <summary>Packet data that has been received</summary>
        public Packet Packet;
        /// <summary>Tick count when this packet was received</summary>
        public int Received;
        /// <summary>Tick count when this packet was scheduled for processing</summary>
        public int StartedHandling;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="agent">Reference to the client this packet came from</param>
        /// <param name="packet">Packet data</param>
        /// <param name="received">Tick count when this packet was received</param>
        public IncomingPacket(LLAgent agent, Packet packet, int received)
        {
            Agent = agent;
            Packet = packet;
            Received = received;
        }
    }
}
