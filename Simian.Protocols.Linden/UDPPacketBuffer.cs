/*
 * Copyright (c) 2006, Clutch, Inc.
 * Original Author: Jeff Cesnik
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without 
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.org nor the names 
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" 
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Net;

namespace Simian.Protocols.Linden
{
    /// <summary>
    /// Wraps an incoming or outgoing UDP packet
    /// </summary>
    public sealed class UDPPacketBuffer
    {
        private static readonly IPEndPoint NullEndPoint = new IPEndPoint(IPAddress.Any, 0);

        /// <summary>Size of the byte array used to store raw packet data</summary>
        public const int BUFFER_SIZE = 4096;
        /// <summary>Raw packet data buffer</summary>
        public byte[] Data;
        /// <summary>Length of the data to transmit</summary>
        public int DataLength;
        /// <summary>EndPoint of the remote host</summary>
        public EndPoint RemoteEndPoint;

        /// <summary>
        /// Create an allocated UDP packet buffer for receiving a packet
        /// </summary>
        public UDPPacketBuffer()
        {
            Data = new byte[UDPPacketBuffer.BUFFER_SIZE];
            // Will be modified later by BeginReceiveFrom()
            RemoteEndPoint = NullEndPoint;
        }

        /// <summary>
        /// Create an allocated UDP packet buffer for sending a packet
        /// </summary>
        /// <param name="endPoint">EndPoint of the remote host</param>
        /// <param name="bufferSize">Size of the buffer to allocate for packet data</param>
        public UDPPacketBuffer(IPEndPoint endPoint, int bufferSize)
        {
            Data = new byte[bufferSize];
            RemoteEndPoint = endPoint;
        }
    }
}
