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
    /// Holds a reference to the <seealso cref="LLUDPClient"/> this packet is
    /// destined for, along with the serialized packet data, sequence number
    /// (if this is a resend), number of times this packet has been resent,
    /// the time of the last resend, and the throttling category for this
    /// packet
    /// </summary>
    public sealed class OutgoingPacket
    {
        /// <summary>Client this packet is destined for</summary>
        public readonly LLAgent Agent;
        /// <summary>Packet data to send</summary>
        public readonly UDPPacketBuffer Buffer;
        /// <summary>Packet type</summary>
        public readonly PacketType Type;

        /// <summary>Throttling category this packet belongs to</summary>
        public ThrottleCategory Category;
        /// <summary>Sequence number of the wrapped packet</summary>
        public uint SequenceNumber;
        /// <summary>Number of times this packet has been resent</summary>
        public int ResendCount;
        /// <summary>When this packet was last sent over the wire</summary>
        public int TickCount;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="agent">Reference to the client this packet is destined for</param>
        /// <param name="buffer">Serialized packet data. If the flags or sequence number
        /// need to be updated, they will be injected directly into this binary buffer</param>
        /// <param name="category">Throttling category for this packet</param>
        /// <param name="type">Packet type</param>
        public OutgoingPacket(LLAgent agent, UDPPacketBuffer buffer, ThrottleCategory category, PacketType type)
        {
            Agent = agent;
            Buffer = buffer;
            Category = category;
            Type = type;
        }
    }
}
