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
using System.Diagnostics;
using System.Text.RegularExpressions;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.Packets;

namespace Simian.Protocols.Linden.Packets
{
    public class J2KImage : IComparable<J2KImage>
    {
        private const int IMAGE_PACKET_SIZE = 1000;
        private const int FIRST_PACKET_SIZE = 600;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        public uint LastSequence;
        public float Priority;
        public uint StartPacket;
        public sbyte DiscardLevel;
        public UUID TextureID;
        public OpenJPEG.J2KLayerInfo[] Layers;
        public C5.PriorityQueueHandle PriorityQueueHandle;

        private LLUDP m_udp;
        private LLAgent m_agent;
        private uint m_currentPacket;
        private bool m_sentInfo;
        private uint m_stopPacket;
        private byte[] m_asset;

        public J2KImage(LLUDP udp, Asset asset, LLAgent agent, sbyte discardLevel, uint startPacket, float priority)
        {
            // Try to get the layer boundary header from the asset headers
            string layerBoundariesHeader = null;
            if (asset.ExtraHeaders != null)
                asset.ExtraHeaders.TryGetValue("X-JPEG2000-Layers", out layerBoundariesHeader);

            m_udp = udp;
            m_agent = agent;

            TextureID = asset.ID;
            DiscardLevel = discardLevel;
            StartPacket = startPacket;
            Priority = priority;

            m_asset = asset.Data;
            Layers = HeaderToLayerBoundaries(layerBoundariesHeader, asset.Data.Length);
        }

        public int CompareTo(J2KImage image)
        {
            return Priority.CompareTo(image.Priority);
        }

        public void UpdateOffsets()
        {
            Debug.Assert(Layers != null && Layers.Length > 0, "Missing Layers array in UpdatePriority()");

            int maxDiscardLevel = Math.Max(0, Layers.Length - 1);

            // Treat initial texture downloads with a DiscardLevel of -1 a request for the highest DiscardLevel
            if (DiscardLevel < 0 && m_stopPacket == 0)
                DiscardLevel = (sbyte)maxDiscardLevel;

            // Clamp at the highest discard level
            DiscardLevel = (sbyte)Math.Min(DiscardLevel, maxDiscardLevel);

            //Calculate the m_stopPacket
            m_stopPacket = (uint)GetPacketForBytePosition(Layers[(Layers.Length - 1) - DiscardLevel].End);

            // This works around an old bug. Not sure if the bug is still present or not
            if (TexturePacketCount() == m_stopPacket + 1)
                m_stopPacket = TexturePacketCount();

            m_currentPacket = StartPacket;
        }

        /// <summary>
        /// Sends packets for this texture to a client until packetsToSend is 
        /// hit or the transfer completes
        /// </summary>
        /// <param name="packetsToSend">Maximum number of packets to send during this call</param>
        /// <param name="packetsSent">Number of packets sent during this call</param>
        /// <returns>True if the transfer completes at the current discard level, otherwise false</returns>
        public bool SendPackets(int packetsToSend, out int packetsSent)
        {
            packetsSent = 0;

            if (m_currentPacket <= m_stopPacket)
            {
                bool sendMore = true;

                if (!m_sentInfo || (m_currentPacket == 0))
                {
                    sendMore = !SendFirstPacket();

                    m_sentInfo = true;
                    ++m_currentPacket;
                    ++packetsSent;
                }
                if (m_currentPacket < 2)
                {
                    m_currentPacket = 2;
                }

                while (sendMore && packetsSent < packetsToSend && m_currentPacket <= m_stopPacket)
                {
                    sendMore = SendPacket();
                    ++m_currentPacket;
                    ++packetsSent;
                }
            }

            return (m_currentPacket > m_stopPacket);
        }

        private bool SendFirstPacket()
        {
            if (m_asset.Length <= FIRST_PACKET_SIZE)
            {
                // We have less then one packet's worth of data
                SendImageFirstPart(1, m_asset);

                m_stopPacket = 0;
                return true;
            }
            else
            {
                // This is going to be a multi-packet texture download
                byte[] firstImageData = new byte[FIRST_PACKET_SIZE];

                try { Buffer.BlockCopy(m_asset, 0, firstImageData, 0, FIRST_PACKET_SIZE); }
                catch (Exception)
                {
                    m_log.ErrorFormat("Texture block copy for the first packet failed. ID={0}, Length={1}", TextureID, m_asset.Length);
                    return true;
                }

                SendImageFirstPart(TexturePacketCount(), firstImageData);
            }
            return false;
        }

        private bool SendPacket()
        {
            bool complete = false;

            int texturePacketCount = TexturePacketCount();
            int currentPosition = CurrentBytePosition();
            int imagePacketSize;

            if ((int)m_currentPacket >= texturePacketCount)
            {
                imagePacketSize = LastPacketSize();
                complete = true;
            }
            else
            {
                imagePacketSize = IMAGE_PACKET_SIZE;
            }

            try
            {
                // It's concievable that the client might request packet one
                // from a one packet image, which is really packet 0,
                // which would leave us with a negative imagePacketSize
                if (imagePacketSize > 0)
                {
                    byte[] imageData = new byte[imagePacketSize];
                    
                    try { Buffer.BlockCopy(m_asset, currentPosition, imageData, 0, imagePacketSize); }
                    catch (Exception ex)
                    {
                        m_log.ErrorFormat("Texture packet block copy failed. ID={0}, Length={1}, CurrentPosition={2}, ImagePacketSize={3}, Exception={4}",
                            TextureID, m_asset.Length, currentPosition, imagePacketSize, ex.Message);
                        return false;
                    }

                    // Send the packet
                    SendImageNextPart((ushort)(m_currentPacket - 1), imageData);
                }

                return !complete;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #region Packet Sending

        private void SendImageFirstPart(ushort packetCount, byte[] imageData)
        {
            ImageDataPacket packet = new ImageDataPacket();

            packet.ImageID.ID = TextureID;
            packet.ImageID.Packets = packetCount;
            packet.ImageID.Size = (uint)m_asset.Length;
            packet.ImageID.Codec = (byte)ImageCodec.J2C;

            packet.ImageData.Data = imageData;

            m_udp.SendPacket(m_agent, packet, ThrottleCategory.Texture, false);
        }

        private void SendImageNextPart(ushort packetNumber, byte[] imageData)
        {
            ImagePacketPacket packet = new ImagePacketPacket();

            packet.ImageID.ID = TextureID;
            packet.ImageID.Packet = packetNumber;
            
            packet.ImageData.Data = imageData;

            m_udp.SendPacket(m_agent, packet, ThrottleCategory.Texture, false);
        }

        #endregion Packet Sending

        #region Byte Position Helpers

        private ushort TexturePacketCount()
        {
            if (m_asset.Length <= FIRST_PACKET_SIZE)
                return 1;

            return (ushort)(((m_asset.Length - FIRST_PACKET_SIZE + IMAGE_PACKET_SIZE - 1) / IMAGE_PACKET_SIZE) + 1);
        }

        private int GetPacketForBytePosition(int bytePosition)
        {
            return ((bytePosition - FIRST_PACKET_SIZE + IMAGE_PACKET_SIZE - 1) / IMAGE_PACKET_SIZE) + 1;
        }

        private int LastPacketSize()
        {
            if (m_currentPacket == 1)
                return m_asset.Length;
            int lastsize = (m_asset.Length - FIRST_PACKET_SIZE) % IMAGE_PACKET_SIZE;
            //If the last packet size is zero, it's really cImagePacketSize, it sits on the boundary
            if (lastsize == 0)
            {
                lastsize = IMAGE_PACKET_SIZE;
            }
            return lastsize;
        }

        private int CurrentBytePosition()
        {
            if (m_currentPacket == 0)
                return 0;
            if (m_currentPacket == 1)
                return FIRST_PACKET_SIZE;

            int result = FIRST_PACKET_SIZE + ((int)m_currentPacket - 2) * IMAGE_PACKET_SIZE;

            if (result < 0)
                result = FIRST_PACKET_SIZE;
            else if (result >= m_asset.Length)
                result = m_asset.Length;

            return result;
        }

        #endregion Byte Position Helpers

        private OpenJPEG.J2KLayerInfo[] HeaderToLayerBoundaries(string layerBoundariesHeader, int dataLength)
        {
            if (String.IsNullOrEmpty(layerBoundariesHeader))
            {
                return new OpenJPEG.J2KLayerInfo[] { new OpenJPEG.J2KLayerInfo { Start = 0, End = dataLength - 1 } };
            }

            const int DEFAULT_LAYER_COUNT = 5;

            if (!String.IsNullOrEmpty(layerBoundariesHeader))
            {
                List<OpenJPEG.J2KLayerInfo> layers = new List<OpenJPEG.J2KLayerInfo>(DEFAULT_LAYER_COUNT);

                try
                {
                    MatchCollection matches = Regex.Matches(layerBoundariesHeader, @"(?<Start>\d+)\-(?<End>\d+);");
                    if (matches != null)
                    {
                        foreach (Match match in matches)
                            layers.Add(new OpenJPEG.J2KLayerInfo { Start = Int32.Parse(match.Groups["Start"].Value), End = Int32.Parse(match.Groups["End"].Value) });
                    }
                }
                catch (Exception ex)
                {
                    m_log.Error("Error decoding layer boundaries header: " + ex.Message);
                }

                if (layers.Count > 0)
                    return layers.ToArray();
            }
            
            return new OpenJPEG.J2KLayerInfo[] { new OpenJPEG.J2KLayerInfo { Start = 0, End = dataLength - 1 } };
        }
    }
}
