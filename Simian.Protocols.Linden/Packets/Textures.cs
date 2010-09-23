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
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace Simian.Protocols.Linden.Packets
{
    [SceneModule("Textures")]
    public class Textures : ISceneModule
    {
        const int TEXTURE_PACKETS_PER_ROUND = 20;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private LLUDP m_udp;
        private IAssetClient m_assetClient;
        private C5.IntervalHeap<J2KImage> m_priorityQueue = new C5.IntervalHeap<J2KImage>(new C5.NaturalComparer<J2KImage>());
        private Dictionary<UUID, J2KImage> m_queuedTextures = new Dictionary<UUID, J2KImage>();

        public void Start(IScene scene)        
        {
            m_scene = scene;
            
            m_assetClient = m_scene.Simian.GetAppModule<IAssetClient>();
            if (m_assetClient == null)
            {
                m_log.Error("Can't initialize texture downloads without an IAssetClient");
                return;
            }
            
            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.RequestImage, RequestImageHandler);

                m_udp.OnQueueEmpty += QueueEmptyHandler;
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.RequestImage, RequestImageHandler);

                m_udp.OnQueueEmpty -= QueueEmptyHandler;
            }
        }

        private void RequestImageHandler(Packet packet, LLAgent agent)
        {
            RequestImagePacket request = (RequestImagePacket)packet;

            for (int i = 0; i < request.RequestImage.Length; i++)
            {
                RequestImagePacket.RequestImageBlock block = request.RequestImage[i];
                EnqueueRequest(agent, block.Image, block.DiscardLevel, block.DownloadPriority, block.Packet, packet.Header.Sequence);
            }
        }

        private void EnqueueRequest(LLAgent agent, UUID textureID, sbyte discardLevel, float priority, uint packetNumber, uint sequenceNumber)
        {
            J2KImage image;

            // Look up this texture download
            lock (m_priorityQueue)
                m_queuedTextures.TryGetValue(textureID, out image);

            if (image != null)
            {
                // Update for an existing texture request
                if (discardLevel == -1 && priority == 0f)
                {
                    //m_log.Debug("[TEX]: (CAN) ID=" + textureID);

                    try
                    {
                        lock (m_priorityQueue)
                        {
                            m_priorityQueue.Delete(image.PriorityQueueHandle);
                            m_queuedTextures.Remove(textureID);
                        }
                    }
                    catch (Exception) { }
                }
                else
                {
                    //m_log.DebugFormat("[TEX]: (UPD) ID={0}: D={1}, S={2}, P={3}", textureID, discardLevel, packetNumber, priority);

                    // Check the packet sequence to make sure this update isn't older than 
                    // one we've already received
                    if (sequenceNumber > image.LastSequence)
                    {
                        // Update the sequence number of the last RequestImage packet
                        image.LastSequence = sequenceNumber;

                        //Update the requested discard level
                        image.DiscardLevel = discardLevel;

                        //Update the requested packet number
                        image.StartPacket = Math.Max(1, packetNumber);

                        //Update the requested priority
                        image.Priority = priority;

                        // Update the start/end offsets for this request
                        image.UpdateOffsets();

                        UpdateImageInQueue(image);
                    }
                }
            }
            else
            {
                // New texture request
                if (discardLevel == -1 && priority == 0f)
                {
                    //m_log.DebugFormat("[TEX]: (IGN) ID={0}", textureID);
                }
                else
                {
                    //m_log.DebugFormat("[TEX]: (NEW) ID={0}: D={1}, S={2}, P={3}", textureID, discardLevel, packetNumber, priority);

                    Asset asset;
                    if (m_assetClient.TryGetAsset(textureID, "image/x-j2c", out asset))
                    {
                        image = new J2KImage(m_udp, asset, agent, discardLevel, Math.Max(1, packetNumber), priority);

                        // Update the start/end offsets for this request
                        image.UpdateOffsets();

                        // Add this download to the priority queue
                        UpdateImageInQueue(image);
                    }
                    else
                    {
                        ImageNotInDatabasePacket missing = new ImageNotInDatabasePacket();
                        missing.ImageID.ID = textureID;
                        m_udp.SendPacket(agent, missing, ThrottleCategory.Asset, true);
                    }
                }
            }
        }

        private void QueueEmptyHandler(LLAgent agent, ThrottleCategoryFlags categories)
        {
            if ((categories & ThrottleCategoryFlags.Texture) == ThrottleCategoryFlags.Texture)
            {
                ProcessImageQueue(TEXTURE_PACKETS_PER_ROUND);
            }
        }

        #region Priority Queue Helpers

        private bool ProcessImageQueue(int packetsToSend)
        {
            int packetsSent = 0;

            while (packetsSent < packetsToSend)
            {
                J2KImage image = GetHighestPriorityImage();

                // If null was returned, the texture priority queue is currently empty
                if (image == null)
                    return false;

                int sent;
                bool imageDone = image.SendPackets(packetsToSend - packetsSent, out sent);
                packetsSent += sent;

                // If the send is complete, destroy any knowledge of this transfer
                if (imageDone)
                    RemoveImageFromQueue(image);
            }

            return m_priorityQueue.Count > 0;
        }

        private J2KImage GetHighestPriorityImage()
        {
            J2KImage image = null;

            lock (m_priorityQueue)
            {
                if (!m_priorityQueue.IsEmpty)
                {
                    try { image = m_priorityQueue.FindMax(); }
                    catch (Exception) { }
                }
            }

            return image;
        }

        private void RemoveImageFromQueue(J2KImage image)
        {
            lock (m_priorityQueue)
            {
                if (m_priorityQueue.Find(image.PriorityQueueHandle, out image))
                {
                    try { m_priorityQueue.Delete(image.PriorityQueueHandle); }
                    catch (Exception) { }
                    m_queuedTextures.Remove(image.TextureID);
                }
            }
        }

        private void UpdateImageInQueue(J2KImage image)
        {
            lock (m_priorityQueue)
            {
                J2KImage existingImage;
                if (m_priorityQueue.Find(image.PriorityQueueHandle, out existingImage))
                {
                    try
                    {
                        m_priorityQueue.Replace(image.PriorityQueueHandle, image);
                        return;
                    }
                    catch { }
                }

                image.PriorityQueueHandle = null;
                m_priorityQueue.Add(ref image.PriorityQueueHandle, image);
                m_queuedTextures[image.TextureID] = image;
            }
        }

        #endregion Priority Queue Helpers
    }
}
