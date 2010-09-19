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
using System.Net;
using System.Threading;
using HttpServer;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.Interfaces;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    /// <summary>
    /// Fired when one or more networking token buckets for an agent have 
    /// capacity but there are no packets to send for those categories
    /// </summary>
    /// <param name="agent">Agent that has empty queue categories</param>
    /// <param name="categories">The queue categories that need more data 
    /// queued (if there is any to send)</param>
    public delegate void QueueEmptyCallback(LLAgent agent, ThrottleCategoryFlags categories);

    /// <summary>
    /// When an agent requests capabilities from a seed capability, this
    /// callback is fired for each of the registered capability builders
    /// </summary>
    /// <param name="agent">Agent that requested the capabilities</param>
    /// <param name="capabilities">This will initially be a list of strings
    /// pointing to null capabilities, which can be filled in by each
    /// registered handler</param>
    public delegate void CreateCapabilitiesCallback(LLAgent agent, IDictionary<string, Uri> capabilities);

    /// <summary>
    /// A thin wrapper class around LLUDPServer that follows the ISceneModule
    /// interface
    /// </summary>
    [SceneModule("LLUDP")]
    public class LLUDP : ISceneModule
    {
        const int DEFAULT_UDP_PORT = 12035;

        private static int m_nextUnusedPort;
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        public event QueueEmptyCallback OnQueueEmpty;

        private LLUDPServer m_udpServer;

        public IPAddress Address { get { return m_udpServer.Address; } }
        public int Port { get { return m_udpServer.Port; } }

        private IPAddress m_masqAddress = null;
        public IPAddress MasqueradeAddress { get { return m_masqAddress; } }

        public int PacketsResent { get { return m_udpServer.m_packetsResent; } }
        public int PacketsSent { get { return m_udpServer.m_packetsSent; } }
        public int PacketsReceived { get { return m_udpServer.m_packetsReceived; } }

        public void Start(IScene scene)
        {
            IPAddress bindAddress = IPAddress.Any;
            int port = DEFAULT_UDP_PORT;
            bool allowAlternatePort = true;

            IConfig config = scene.Config.Configs["LindenRegion"];
            if (config != null)
            {
                port = config.GetInt("Port", DEFAULT_UDP_PORT);
                allowAlternatePort = config.GetBoolean("AllowAlternatePort", true);
            }

            config = scene.Config.Configs["LLUDP"];
            if (config != null)
            {
                IPAddress.TryParse(config.GetString("BindAddress", "0.0.0.0"), out bindAddress);
                IPAddress.TryParse(config.GetString("MasqueradeAddress", String.Empty), out m_masqAddress);
            }

            if (bindAddress.Equals(IPAddress.Any))
                bindAddress = Util.GetLocalInterface();

            IScheduler scheduler = scene.Simian.GetAppModule<IScheduler>();
            if (scheduler == null)
            {
                m_log.Error("Cannot start LLUDP server without an IScheduler");
                throw new InvalidOperationException();
            }

            if (allowAlternatePort && m_nextUnusedPort != 0)
                port = m_nextUnusedPort;

            m_udpServer = new LLUDPServer(this, scene, bindAddress, port, scene.Config, scheduler);

            // Loop until we successfully bind to a port or run out of options
            while (true)
            {
                //m_log.Debug("Trying to bind LLUDP server to " + bindAddress + ":" + port);

                try
                {
                    m_udpServer.Address = bindAddress;
                    m_udpServer.Port = port;
                    m_udpServer.Start();

                    m_log.Info("Bound LLUDP server to " + bindAddress + ":" + port);

                    m_nextUnusedPort = port + 1;
                    IPAddress address = m_masqAddress == null ? bindAddress : m_masqAddress;

                    scene.ExtraData["ExternalAddress"] = OSD.FromString(address.ToString());
                    scene.ExtraData["ExternalPort"] = OSD.FromInteger(port);

                    break;
                }
                catch (System.Net.Sockets.SocketException)
                {
                    if (allowAlternatePort)
                    {
                        ++port;
                    }
                    else
                    {
                        m_log.Error("Failed to bind LLUDP server to port " + port);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    m_log.Error("Failed to bind LLUDP server to any port: " + ex.Message);
                    break;
                }
            }

            scene.AddCommandHandler("packetlog", PacketLogCommandHandler);
        }

        public void Stop()
        {
            m_udpServer.Stop();
            m_udpServer.Scene.RemoveCommandHandler("packetlog");
        }

        public bool TryGetAgent(UUID agentID, out LLAgent agent)
        {
            return m_udpServer.TryGetAgent(agentID, out agent);
        }

        public void AddPacketHandler(PacketType packetType, PacketCallback eventHandler)
        {
            m_udpServer.PacketEvents.RegisterEvent(packetType, eventHandler);
        }

        public void RemovePacketHandler(PacketType packetType, PacketCallback eventHandler)
        {
            m_udpServer.PacketEvents.UnregisterEvent(packetType, eventHandler);
        }

        public void SendPacket(LLAgent agent, Packet packet, ThrottleCategory category, bool allowSplitting)
        {
            m_udpServer.SendPacket(agent, packet, category, allowSplitting);
        }

        public void SendPacketData(LLAgent agent, byte[] data, PacketType type, ThrottleCategory category)
        {
            m_udpServer.SendPacketData(agent, data, type, category);
        }

        public void BroadcastPacket(Packet packet, ThrottleCategory category, bool sendToPausedAgents, bool allowSplitting)
        {
            m_udpServer.BroadcastPacket(packet, category, sendToPausedAgents, allowSplitting);
        }

        public bool EnableCircuit(UserSession session, Vector3 startPosition, Vector3 lookAt, bool isChildAgent, out Uri seedCapability)
        {
            return m_udpServer.EnableCircuit(session, startPosition, lookAt, isChildAgent, out seedCapability);
        }

        public void FireQueueEmpty(LLAgent agent, ThrottleCategoryFlags categories)
        {
            QueueEmptyCallback callback = OnQueueEmpty;
            if (callback != null)
                callback(agent, categories);
        }

        private void PacketLogCommandHandler(string command, string[] args, bool printHelp)
        {
            if (printHelp || args.Length == 0)
            {
                Console.WriteLine("Toggles packet logging for a scene\n\nExample: packetlog [true/false]");
            }
            else
            {
                if (args[0].Equals("true", StringComparison.InvariantCultureIgnoreCase) ||
                    args[0].Equals("on", StringComparison.InvariantCultureIgnoreCase) ||
                    args[0].Equals("1", StringComparison.InvariantCultureIgnoreCase))
                {
                    m_log.Info("Enabling packet logging for " + m_udpServer.Scene.Name);
                    m_udpServer.LoggingEnabled = true;
                }
                else
                {
                    m_log.Info("Disabling packet logging for " + m_udpServer.Scene.Name);
                    m_udpServer.LoggingEnabled = false;
                }
            }
        }
    }

    /// <summary>
    /// The actual LLUDP server implementation
    /// </summary>
    public class LLUDPServer : UDPBase
    {
        /// <summary>
        /// A lightweight structure for handling our own timers
        /// </summary>
        private struct TimedEvent
        {
            public readonly int Interval;
            public int Elapsed;

            public TimedEvent(int interval)
            {
                Interval = interval;
                Elapsed = 0;
            }
        }

        /// <summary>Maximum transmission unit, or UDP packet size, for the LLUDP protocol</summary>
        public const int MTU = 1400;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        /// <summary>Reference to the scene this UDP server is attached to</summary>
        public readonly IScene Scene;

        /// <summary>A reference to a thread scheduler</summary>
        internal IScheduler Scheduler;
        /// <summary>Collection of packet handling callbacks</summary>
        internal PacketEventDictionary PacketEvents;

        /// <summary>Enables/disables logging of all incoming and outgoing packets</summary>
        internal bool LoggingEnabled;

        /// <summary>Reference to the LLUDP class that instantiated this class</summary>
        private readonly LLUDP m_udp;
        /// <summary>HTTP module, used to create seed capabilities</summary>
        private IHttpServer m_httpServer;
        /// <summary>Collection of currently connected clients</summary>
        private readonly MapsAndArray<UUID, IPEndPoint, LLAgent> m_clients = new MapsAndArray<UUID, IPEndPoint, LLAgent>();
        /// <summary>Incoming packets that are awaiting handling</summary>
        private readonly BlockingQueue<IncomingPacket> m_packetInbox = new BlockingQueue<IncomingPacket>();
        /// <summary>Bandwidth throttle for this UDP server</summary>
        private readonly TokenBucket m_throttle;
        /// <summary>Bandwidth throttle rates for this UDP server</summary>
        private readonly ThrottleRates m_throttleRates;
        /// <summary>The size of the receive buffer for the UDP socket. This value
        /// is passed up to the operating system and used in the system networking
        /// stack. Use zero to leave this value as the default</summary>
        private readonly int m_recvBufferSize;
        /// <summary>Flag to process packets asynchronously or synchronously</summary>
        private readonly bool m_asyncPacketHandling = true;

        /// <summary>Environment.TickCount of the last time the outgoing packet 
        /// handler executed</summary>
        private int m_tickLastOutgoingPacketHandler;
        /// <summary>Tracks elapsed time since packet resending was last
        /// checked</summary>
        private TimedEvent m_resendTimer;
        /// <summary>Tracks elapsed time since ACK sending was last checked</summary>
        private TimedEvent m_ackTimer;
        /// <summary>Tracks elapsed time since a ping was last sent</summary>
        private TimedEvent m_pingTimer;

        /// <summary>Tracks whether or not a packet was sent each round so we know
        /// whether or not to sleep</summary>
        private bool m_packetSent;
        /// <summary>Flag to signal when clients should check for resends</summary>
        private bool m_resendUnacked;
        /// <summary>Flag to signal when clients should send ACKs</summary>
        private bool m_sendAcks;
        /// <summary>Flag to signal when clients should send pings</summary>
        private bool m_sendPing;

        /// <summary>Default retransmission timeout</summary>
        private int m_defaultRTO = 1000 * 3;
        /// <summary>Maximum retransmission timeout</summary>
        private int m_maxRTO = 1000 * 30;

        /// <summary>Stats tracking for resent packets</summary>
        internal int m_packetsResent;
        /// <summary>Stats tracking for sent packets</summary>
        internal int m_packetsSent;
        /// <summary>Stats tracking for received packets</summary>
        internal int m_packetsReceived;

        internal IPAddress Address
        {
            get { return base.m_localBindAddress; }
            set { base.m_localBindAddress = value; }
        }
        internal int Port
        {
            get { return base.m_udpPort; }
            set { base.m_udpPort = value; }
        }

        public LLUDPServer(LLUDP udp, IScene scene, IPAddress bindAddress, int port, IConfigSource configSource, IScheduler scheduler)
            : base(bindAddress, port)
        {
            m_udp = udp;
            Scene = scene;
            Scheduler = scheduler;

            IConfig throttleConfig = configSource.Configs["LLUDP"];
            m_throttleRates = new ThrottleRates(LLUDPServer.MTU, throttleConfig);

            m_resendTimer = new TimedEvent(100);
            m_ackTimer = new TimedEvent(500);
            m_pingTimer = new TimedEvent(5000);

            m_httpServer = Scene.Simian.GetAppModule<IHttpServer>();

            IConfig config = configSource.Configs["LLUDP"];
            if (config != null)
            {
                m_asyncPacketHandling = config.GetBoolean("AsyncPacketHandling", true);
                m_recvBufferSize = config.GetInt("SocketReceiveBufferSize", 0);
            }

            m_throttle = new TokenBucket(null, m_throttleRates.SceneTotalLimit, m_throttleRates.SceneTotal);

            PacketEvents = new PacketEventDictionary(Scheduler);

            Scene.OnPresenceRemove += PresenceRemoveHandler;
        }

        public void Start()
        {
            base.Start(m_recvBufferSize, m_asyncPacketHandling);

            // Start the packet processing threads
            Scheduler.StartThread(PacketHandler, "LLUDP (" + Scene.Name + ")", ThreadPriority.Normal, false);
        }

        public new void Stop()
        {
            base.Stop();
        }

        #region Public Methods

        public bool TryGetAgent(UUID agentID, out LLAgent agent)
        {
            return m_clients.TryGetValue(agentID, out agent);
        }

        public void BroadcastPacket(Packet packet, ThrottleCategory category, bool sendToPausedAgents, bool allowSplitting)
        {
            // CoarseLocationUpdate and AvatarGroupsReply packets cannot be split in an automated way
            if ((packet.Type == PacketType.CoarseLocationUpdate || packet.Type == PacketType.AvatarGroupsReply) && allowSplitting)
                allowSplitting = false;

            if (allowSplitting && packet.HasVariableBlocks)
            {
                byte[][] datas = packet.ToBytesMultiple();
                int packetCount = datas.Length;

                if (packetCount < 1)
                    m_log.Error("[LLUDPSERVER]: Failed to split " + packet.Type + " with estimated length " + packet.Length);

                for (int i = 0; i < packetCount; i++)
                {
                    byte[] data = datas[i];
                    m_clients.ForEach(delegate(LLAgent agent) { SendPacketData(agent, data, packet.Type, category); });
                }
            }
            else
            {
                byte[] data = packet.ToBytes();
                m_clients.ForEach(delegate(LLAgent agent) { SendPacketData(agent, data, packet.Type, category); });
            }
        }

        public void SendPacket(LLAgent agent, Packet packet, ThrottleCategory category, bool allowSplitting)
        {
            // CoarseLocationUpdate and AvatarGroupsReply packets cannot be split in an automated way
            if ((packet.Type == PacketType.CoarseLocationUpdate || packet.Type == PacketType.AvatarGroupsReply) && allowSplitting)
                allowSplitting = false;

            try
            {
                if (allowSplitting && packet.HasVariableBlocks)
                {
                    byte[][] datas = packet.ToBytesMultiple();
                    int packetCount = datas.Length;

                    if (packetCount < 1)
                        m_log.Error("[LLUDPSERVER]: Failed to split " + packet.Type + " with estimated length " + packet.Length);

                    for (int i = 0; i < packetCount; i++)
                    {
                        byte[] data = datas[i];
                        SendPacketData(agent, data, packet.Type, category);
                    }
                }
                else
                {
                    byte[] data = packet.ToBytes();
                    SendPacketData(agent, data, packet.Type, category);
                }
            }
            catch (NullReferenceException)
            {
                System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(1, true);
                m_log.Error("An invalid " + packet.Type + " packet was built in:" + Environment.NewLine + trace.ToString());
            }
        }

        public void SendPacketData(LLAgent agent, byte[] data, PacketType type, ThrottleCategory category)
        {
            int dataLength = data.Length;
            bool doZerocode = (data[0] & Helpers.MSG_ZEROCODED) != 0;
            bool doCopy = true;

            // Frequency analysis of outgoing packet sizes shows a large clump of packets at each end of the spectrum.
            // The vast majority of packets are less than 200 bytes, although due to asset transfers and packet splitting
            // there are a decent number of packets in the 1000-1140 byte range. We allocate one of two sizes of data here
            // to accomodate for both common scenarios and provide ample room for ACK appending in both
            int bufferSize = (dataLength > 180)
                ? LLUDPServer.MTU
                : 200;

            UDPPacketBuffer buffer = new UDPPacketBuffer(agent.RemoteEndPoint, bufferSize);

            // Zerocode if needed
            if (doZerocode)
            {
                try
                {
                    dataLength = Helpers.ZeroEncode(data, dataLength, buffer.Data);
                    doCopy = false;
                }
                catch (IndexOutOfRangeException)
                {
                    // The packet grew larger than the bufferSize while zerocoding.
                    // Remove the MSG_ZEROCODED flag and send the unencoded data
                    // instead
                    m_log.Debug("Packet exceeded buffer size during zerocoding for " + type + ". DataLength=" + dataLength +
                        " and BufferLength=" + buffer.Data.Length + ". Removing MSG_ZEROCODED flag");
                    data[0] = (byte)(data[0] & ~Helpers.MSG_ZEROCODED);
                }
            }

            // If the packet data wasn't already copied during zerocoding, copy it now
            if (doCopy)
            {
                if (dataLength > buffer.Data.Length)
                {
                    m_log.Error("Packet exceeded buffer size! This could be an indication of packet assembly not obeying the MTU. Type=" +
                        type + ", DataLength=" + dataLength + ", BufferLength=" + buffer.Data.Length);
                    buffer.Data = new byte[dataLength];
                }

                Buffer.BlockCopy(data, 0, buffer.Data, 0, dataLength);
            }

            buffer.DataLength = dataLength;

            #region Queue or Send

            OutgoingPacket outgoingPacket = new OutgoingPacket(agent, buffer, category, type);

            if (!agent.EnqueueOutgoing(outgoingPacket))
                SendPacketFinal(outgoingPacket);

            #endregion Queue or Send
        }

        public void SendAcks(LLAgent agent)
        {
            const int MAX_ACKS_PER_PACKET = Byte.MaxValue;

            uint ack;
            if (agent.PendingAcks.TryDequeue(out ack))
            {
                List<PacketAckPacket.PacketsBlock> blocks = new List<PacketAckPacket.PacketsBlock>(agent.PendingAcks.Count);
                PacketAckPacket.PacketsBlock block = new PacketAckPacket.PacketsBlock();
                block.ID = ack;
                blocks.Add(block);

                int count = 1;

                while (count < MAX_ACKS_PER_PACKET && agent.PendingAcks.TryDequeue(out ack))
                {
                    block = new PacketAckPacket.PacketsBlock();
                    block.ID = ack;
                    blocks.Add(block);

                    ++count;
                }

                PacketAckPacket packet = new PacketAckPacket();
                packet.Header.Reliable = false;
                packet.Packets = blocks.ToArray();

                SendPacket(agent, packet, ThrottleCategory.Unknown, false);
            }
        }

        public void SendPing(LLAgent agent)
        {
            StartPingCheckPacket pc = new StartPingCheckPacket();
            pc.Header.Reliable = false;

            pc.PingID.PingID = (byte)agent.CurrentPingSequence++;
            // We *could* get OldestUnacked, but it would hurt performance and not provide any benefit
            pc.PingID.OldestUnacked = 0;

            SendPacket(agent, pc, ThrottleCategory.Unknown, false);
        }

        public void CompletePing(LLAgent agent, byte pingID)
        {
            CompletePingCheckPacket completePing = new CompletePingCheckPacket();
            completePing.Header.Reliable = false;
            completePing.PingID.PingID = pingID;
            SendPacket(agent, completePing, ThrottleCategory.Unknown, false);
        }

        public void ResendUnacked(LLAgent agent)
        {
            //FIXME: Make this an .ini setting
            const int AGENT_TIMEOUT_MS = 1000 * 60;

            // Disconnect an agent if no packets are received for some time
            if (Util.TickCount() - agent.TickLastPacketReceived > AGENT_TIMEOUT_MS)
            {
                m_log.Warn("Ack timeout, disconnecting " + agent.ID);

                ShutdownClient(agent);
                return;
            }

            // Get a list of all of the packets that have been sitting unacked longer than udpClient.RTO
            List<OutgoingPacket> expiredPackets = agent.NeedAcks.GetExpiredPackets(agent.RTO);

            if (expiredPackets != null)
            {
                //m_log.Debug("[LLUDPSERVER]: Resending " + expiredPackets.Count + " packets to " + udpClient.AgentID + ", RTO=" + udpClient.RTO);

                // Exponential backoff of the retransmission timeout
                agent.BackoffRTO();

                // Resend packets
                for (int i = 0; i < expiredPackets.Count; i++)
                {
                    OutgoingPacket outgoingPacket = expiredPackets[i];

                    //m_log.DebugFormat("[LLUDPSERVER]: Resending packet #{0} (attempt {1}), {2}ms have passed",
                    //    outgoingPacket.SequenceNumber, outgoingPacket.ResendCount, Environment.TickCount - outgoingPacket.TickCount);

                    // Set the resent flag
                    outgoingPacket.Buffer.Data[0] = (byte)(outgoingPacket.Buffer.Data[0] | Helpers.MSG_RESENT);
                    outgoingPacket.Category = ThrottleCategory.Resend;

                    // Bump up the resend count on this packet
                    Interlocked.Increment(ref outgoingPacket.ResendCount);
                    Interlocked.Increment(ref m_packetsResent);

                    // Requeue or resend the packet
                    if (!agent.EnqueueOutgoing(outgoingPacket))
                        SendPacketFinal(outgoingPacket);
                }
            }
        }

        public void FireQueueEmpty(LLAgent agent, ThrottleCategoryFlags categories)
        {
            m_udp.FireQueueEmpty(agent, categories);
        }

        #endregion Public Methods

        #region Packet Handling

        internal bool EnableCircuit(UserSession session, Vector3 startPosition, Vector3 lookAt, bool isChildAgent, out Uri seedCapability)
        {
            LLAgent client;
            if (m_clients.TryGetValue(session.User.ID, out client))
            {
                if (!client.IsChildPresence && !isChildAgent)
                {
                    // Root agent trying to come in that we already have a root agent for.
                    // Boot out the existing agent
                    Scene.EntityRemove(this, client);
                    m_clients.Remove(client.ID, client.RemoteEndPoint);

                    // Create a new LLAgent
                    client = CreateLLAgent(session, startPosition, lookAt, isChildAgent);
                    seedCapability = client.SeedCapability;
                    return true;
                }
                else if (!client.IsChildPresence && isChildAgent)
                {
                    m_log.Warn(Scene.Name + " trying to enable child circuit for a root agent, ignoring for " + session.User.Name);
                }
                else if (client.IsChildPresence && !isChildAgent)
                {
                    // Child agent is being upgraded to a root agent
                    m_log.Info("Upgrading child agent " + client.Name  + " to root agent in " + Scene.Name + ", pos=" + startPosition + ", vel=" + client.Velocity);
                    
                    // Mark this agent as a root agent
                    client.IsChildPresence = false;
                    // Set the current position
                    client.RelativePosition = startPosition;
                    // Set the current rotation
                    lookAt.Z = 0.0f;
                    Matrix4 lookAtMatrix = Matrix4.CreateLookAt(Vector3.Zero, lookAt, Vector3.UnitZ);
                    client.RelativeRotation = lookAtMatrix.GetQuaternion();

                    seedCapability = client.SeedCapability;
                    return true;
                }
                else
                {
                    // We already know about this child agent
                    m_log.Info("Re-enabling child circuit for " + client.Name);

                    // Set the current position
                    client.RelativePosition = startPosition;
                    // Set the current rotation
                    lookAt.Z = 0.0f;
                    Matrix4 lookAtMatrix = Matrix4.CreateLookAt(Vector3.Zero, lookAt, Vector3.UnitZ);
                    client.RelativeRotation = lookAtMatrix.GetQuaternion();

                    seedCapability = client.SeedCapability;
                    return true;
                }
            }
            else
            {
                // Create a new LLAgent
                client = CreateLLAgent(session, startPosition, lookAt, isChildAgent);
                seedCapability = client.SeedCapability;
                return true;
            }

            seedCapability = null;
            return false;
        }

        private LLAgent CreateLLAgent(UserSession session, Vector3 startPosition, Vector3 lookAt, bool isChildAgent)
        {
            LLAgent client = new LLAgent(this, m_throttleRates, m_throttle, session.GetField("CircuitCode").AsUInteger(),
                    session.User.ID, session.SessionID, session.SecureSessionID, null, m_defaultRTO, m_maxRTO, isChildAgent);

            // Set the verified flag
            client.IsVerified = (session.User.AccessLevel > 0);
            // Set the agent name
            client.Name = session.User.Name;
            // Set the starting position
            client.RelativePosition = startPosition;
            // Set the starting rotation
            lookAt.Z = 0.0f;
            Matrix4 lookAtMatrix = Matrix4.CreateLookAt(Vector3.Zero, lookAt, Vector3.UnitZ);
            client.RelativeRotation = lookAtMatrix.GetQuaternion();

            m_clients.Add(client.ID, client.RemoteEndPoint, client);

            // Create a seed capability
            if (m_httpServer != null)
                client.SeedCapability = this.Scene.Capabilities.AddCapability(session.User.ID, true, this.Scene.ID, "region_seed_capability");
            else
                client.SeedCapability = new Uri("http://localhost:0");

            return client;
        }

        internal void SendPacketFinal(OutgoingPacket outgoingPacket)
        {
            const int MAX_APPENDED_ACKS = 250;

            UDPPacketBuffer buffer = outgoingPacket.Buffer;
            byte flags = buffer.Data[0];
            bool isResend = (flags & Helpers.MSG_RESENT) != 0;
            bool isReliable = (flags & Helpers.MSG_RELIABLE) != 0;
            bool isZerocoded = (flags & Helpers.MSG_ZEROCODED) != 0;
            LLAgent agent = outgoingPacket.Agent;

            if (!agent.IsConnected || buffer.RemoteEndPoint == null)
            {
                m_log.Debug("Dropping " + buffer.DataLength + " byte packet to client we have no route to");
                return;
            }

            int dataLength = buffer.DataLength;

            #region ACK Appending

            // NOTE: I'm seeing problems with some viewers when ACKs are appended to zerocoded packets so I've disabled that here
            if (!isZerocoded && outgoingPacket.Type != PacketType.PacketAck)
            {
                // Keep appending ACKs until there is no room left in the buffer or there are
                // no more ACKs to append
                byte ackCount = 0;
                uint ack;
                while (dataLength + 5 < buffer.Data.Length && ackCount < MAX_APPENDED_ACKS && agent.PendingAcks.TryDequeue(out ack))
                {
                    Utils.UIntToBytesBig(ack, buffer.Data, dataLength);
                    dataLength += 4;
                    ++ackCount;
                }

                if (ackCount > 0)
                {
                    // Set the last byte of the packet equal to the number of appended ACKs
                    buffer.Data[dataLength++] = ackCount;
                    // Set the appended ACKs flag on this packet
                    buffer.Data[0] |= Helpers.MSG_APPENDED_ACKS;
                }
            }

            #endregion ACK Appending

            buffer.DataLength = dataLength;

            #region Sequence Number Assignment

            if (!isResend)
            {
                // Not a resend, assign a new sequence number
                uint sequenceNumber = (uint)Interlocked.Increment(ref agent.CurrentSequence);
                Utils.UIntToBytesBig(sequenceNumber, buffer.Data, 1);
                outgoingPacket.SequenceNumber = sequenceNumber;

                if (isReliable)
                {
                    // Add this packet to the list of ACK responses we are waiting on from the server
                    agent.NeedAcks.Add(outgoingPacket);
                }
            }

            #endregion Sequence Number Assignment

            // Stats tracking
            Interlocked.Increment(ref agent.PacketsSent);
            Interlocked.Increment(ref m_packetsSent);
            if (isReliable)
                Interlocked.Add(ref agent.UnackedBytes, outgoingPacket.Buffer.DataLength);

            if (LoggingEnabled)
                m_log.Debug("<-- (" + buffer.RemoteEndPoint + ") " + outgoingPacket.Type);

            // Put the UDP payload on the wire
            Send(buffer);

            // Keep track of when this packet was sent out (right now)
            outgoingPacket.TickCount = Util.TickCount();

            //m_log.Debug("Sent " + outgoingPacket.Buffer.DataLength + " byte " + outgoingPacket.Category + " packet");
        }

        protected override void PacketReceived(UDPPacketBuffer buffer)
        {
            // Debugging/Profiling
            //try { Thread.CurrentThread.Name = "PacketReceived (" + m_scene.RegionInfo.RegionName + ")"; }
            //catch (Exception) { }

            Packet packet = null;
            int packetEnd = buffer.DataLength - 1;
            IPEndPoint address = (IPEndPoint)buffer.RemoteEndPoint;

            int now = Util.TickCount();

            #region Decoding

            try
            {
                packet = Packet.BuildPacket(buffer.Data, ref packetEnd,
                    // Only allocate a buffer for zerodecoding if the packet is zerocoded
                    ((buffer.Data[0] & Helpers.MSG_ZEROCODED) != 0) ? new byte[4096] : null);
            }
            catch (MalformedDataException)
            {
                m_log.ErrorFormat("Malformed data, cannot parse packet from {0}:\n{1}",
                    buffer.RemoteEndPoint, Utils.BytesToHexString(buffer.Data, buffer.DataLength, null));
            }

            // Fail-safe check
            if (packet == null)
            {
                m_log.Warn("Couldn't build a message from incoming data " + buffer.DataLength +
                    " bytes long from " + buffer.RemoteEndPoint);
                return;
            }

            #endregion Decoding

            #region Packet to Client Mapping

            // UseCircuitCode handling
            if (packet.Type == PacketType.UseCircuitCode)
            {
                m_log.Debug("Handling UseCircuitCode packet from " + buffer.RemoteEndPoint);
                HandleUseCircuitCode(buffer, (UseCircuitCodePacket)packet, now);
                return;
            }

            // Determine which agent this packet came from
            LLAgent agent;
            if (!m_clients.TryGetValue(address, out agent))
            {
                m_log.Debug("Received a " + packet.Type + " packet from an unrecognized source: " + address + " in " + Scene.Name);
                return;
            }

            if (!agent.IsConnected)
                return;

            #endregion Packet to Client Mapping

            #region Stats Tracking

            Interlocked.Increment(ref m_packetsReceived);
            Interlocked.Increment(ref agent.PacketsReceived);
            agent.TickLastPacketReceived = now;

            if (LoggingEnabled)
                m_log.Debug("--> (" + buffer.RemoteEndPoint + ") " + packet.Type);

            #endregion Stats Tracking

            #region ACK Receiving

            // Handle appended ACKs
            if (packet.Header.AppendedAcks && packet.Header.AckList != null)
            {
                for (int i = 0; i < packet.Header.AckList.Length; i++)
                    agent.NeedAcks.Remove(packet.Header.AckList[i], now, packet.Header.Resent);
            }

            // Handle PacketAck packets
            if (packet.Type == PacketType.PacketAck)
            {
                PacketAckPacket ackPacket = (PacketAckPacket)packet;

                for (int i = 0; i < ackPacket.Packets.Length; i++)
                    agent.NeedAcks.Remove(ackPacket.Packets[i].ID, now, packet.Header.Resent);

                // We don't need to do anything else with PacketAck packets
                return;
            }

            #endregion ACK Receiving

            #region Incoming Packet Accounting

            // Check the archive of received reliable packet IDs to see whether we already received this packet
            if (packet.Header.Reliable && !agent.PacketArchive.TryEnqueue(packet.Header.Sequence))
            {
                if (packet.Header.Resent)
                    m_log.Debug("Received a resend of already processed packet #" + packet.Header.Sequence + ", type: " + packet.Type);
                else
                    m_log.Warn("Received a duplicate (not marked as resend) of packet #" + packet.Header.Sequence + ", type: " + packet.Type);

                // ACK this packet immediately to avoid further resends of this same packet
                SendAckImmediate((IPEndPoint)buffer.RemoteEndPoint, packet.Header.Sequence);

                // Avoid firing a callback twice for the same packet
                return;
            }

            #endregion Incoming Packet Accounting

            #region ACK Sending

            if (packet.Header.Reliable)
            {
                agent.PendingAcks.Enqueue(packet.Header.Sequence);
            }

            #endregion ACK Sending

            #region Ping Check Handling

            if (packet.Type == PacketType.StartPingCheck)
            {
                // We don't need to do anything else with ping checks
                StartPingCheckPacket startPing = (StartPingCheckPacket)packet;
                CompletePing(agent, startPing.PingID.PingID);
                return;
            }
            else if (packet.Type == PacketType.CompletePingCheck)
            {
                // We don't currently track client ping times
                return;
            }

            #endregion Ping Check Handling

            // Inbox insertion
            m_packetInbox.Enqueue(new IncomingPacket(agent, packet, now));
        }

        private void HandleUseCircuitCode(UDPPacketBuffer buffer, UseCircuitCodePacket packet, int now)
        {
            IPEndPoint remoteEndPoint = (IPEndPoint)buffer.RemoteEndPoint;

            LLAgent agent;
            if (m_clients.TryGetValue(packet.CircuitCode.ID, out agent))
            {
                // Update the remoteEndPoint for this agent
                m_clients.UpdateKey2(agent.ID, agent.RemoteEndPoint, remoteEndPoint);
                agent.RemoteEndPoint = remoteEndPoint;

                // Acknowledge the UseCircuitCode packet
                SendAckImmediate(remoteEndPoint, packet.Header.Sequence);

                // Fire any callbacks registered for this packet
                IncomingPacket incomingPacket = new IncomingPacket(agent, packet, now);
                incomingPacket.StartedHandling = now;
                PacketEvents.BeginRaiseEvent(incomingPacket);
            }
            else
            {
                m_log.Error("Failed to add new client " + packet.CircuitCode.ID + " connecting from " + remoteEndPoint);
            }
        }

        private void SendAckImmediate(IPEndPoint remoteEndpoint, uint sequenceNumber)
        {
            PacketAckPacket ack = new PacketAckPacket();
            ack.Header.Reliable = false;
            ack.Packets = new PacketAckPacket.PacketsBlock[1];
            ack.Packets[0] = new PacketAckPacket.PacketsBlock();
            ack.Packets[0].ID = sequenceNumber;

            byte[] packetData = ack.ToBytes();
            int length = packetData.Length;

            UDPPacketBuffer buffer = new UDPPacketBuffer(remoteEndpoint, length);
            buffer.DataLength = length;

            Buffer.BlockCopy(packetData, 0, buffer.Data, 0, length);

            Send(buffer);
        }

        #endregion Packet Handling

        #region Client Handling

        private void ShutdownClient(LLAgent agent)
        {
            // Remove this client from the scene
            agent.Shutdown();
            m_clients.Remove(agent.ID, agent.RemoteEndPoint);
        }

        private void PresenceRemoveHandler(object sender, PresenceArgs e)
        {
            if (e.Presence is LLAgent)
            {
                // Remove all capabilities associated with this client
                if (m_httpServer != null)
                    this.Scene.Capabilities.RemoveCapabilities(e.Presence.ID);

                // Remove the UDP client reference
                LLAgent agent = (LLAgent)e.Presence;
                if (m_clients.Remove(agent.ID, agent.RemoteEndPoint))
                    m_log.Debug("Removed client reference from the LLUDP server");
            }
            else
            {
                m_log.Warn("PresenceRemoveHandler called for non-LLAgent: " + e.Presence);
            }
        }

        #endregion Client Handling

        #region Packet Processing

        private void PacketHandler()
        {
            Action<LLAgent> clientPacketHandler = ClientOutgoingPacketHandler;

            while (base.IsRunning)
            {
                try
                {
                    // Time keeping
                    int now = Util.TickCount();
                    int elapsed = now - m_tickLastOutgoingPacketHandler;
                    m_tickLastOutgoingPacketHandler = now;

                    // Maximum time to wait dequeuing an incoming packet. Used
                    // to put this thread to sleep when there is little or no
                    // activity
                    int dequeueTimeout = 0;

                    #region Outgoing Packets

                    m_packetSent = false;
                    
                    #region Update Timers

                    m_resendUnacked = false;
                    m_sendAcks = false;
                    m_sendPing = false;

                    m_resendTimer.Elapsed += elapsed;
                    m_ackTimer.Elapsed += elapsed;
                    m_pingTimer.Elapsed += elapsed;

                    if (m_resendTimer.Elapsed >= m_resendTimer.Interval)
                    {
                        m_resendUnacked = true;
                        m_resendTimer.Elapsed = 0;
                    }
                    if (m_ackTimer.Elapsed >= m_ackTimer.Interval)
                    {
                        m_sendAcks = true;
                        m_ackTimer.Elapsed = 0;
                    }
                    if (m_pingTimer.Elapsed >= m_pingTimer.Interval)
                    {
                        m_sendPing = true;
                        m_pingTimer.Elapsed = 0;
                    }

                    #endregion Update Timers

                    // Handle outgoing packets, resends, acknowledgements, and pings for each
                    // client. m_packetSent will be set to true if a packet is sent
                    m_clients.ForEach(clientPacketHandler);

                    // If nothing was sent, wait up to the minimum amount of time before a
                    // token bucket could get more tokens, if we have clients connected.
                    // Otherwise, do a long wait
                    if (!m_packetSent)
                    {
                        if (m_clients.Count > 0)
                            dequeueTimeout = (int)Scene.Simian.TickCountResolution;
                        else
                            dequeueTimeout = Simian.LONG_SLEEP_INTERVAL;
                    }

                    #endregion Outgoing Packets

                    #region Incoming Packets

                    IncomingPacket incomingPacket = null;

                    if (m_packetInbox.Dequeue(dequeueTimeout, ref incomingPacket))
                    {
                        Packet packet = incomingPacket.Packet;
                        LLAgent agent = incomingPacket.Agent;

                        // Record the time we started processing this packet
                        incomingPacket.StartedHandling = Util.TickCount();

                        // Sanity check
                        if (packet == null || agent == null)
                        {
                            m_log.WarnFormat("Processing a packet with incomplete state. Packet=\"{0}\", LLAgent=\"{1}\"",
                                packet, agent);
                        }

                        PacketEvents.BeginRaiseEvent(incomingPacket);
                    }

                    #endregion Incoming Packets
                }
                catch (Exception ex)
                {
                    m_log.Error("Error in the packet handler loop: " + ex.Message, ex);
                }

                Scheduler.ThreadKeepAlive();
            }

            if (m_packetInbox.Count > 0)
                m_log.Warn("IncomingPacketHandler is shutting down, dropping " + m_packetInbox.Count + " packets");
            m_packetInbox.Clear();

            Scheduler.RemoveThread();
        }

        private void ClientOutgoingPacketHandler(LLAgent agent)
        {
            try
            {
                if (agent.IsConnected && agent.RemoteEndPoint != null)
                {
                    if (m_resendUnacked)
                        ResendUnacked(agent);

                    if (m_sendAcks)
                        SendAcks(agent);

                    if (m_sendPing)
                        SendPing(agent);

                    // Dequeue any outgoing packets that are within the throttle limits
                    if (agent.DequeueOutgoing())
                        m_packetSent = true;
                }
            }
            catch (Exception ex)
            {
                m_log.Error("OutgoingPacketHandler iteration for " + agent.ID +
                    " threw an exception: " + ex.Message, ex);
            }
        }

        #endregion Packet Processing
    }
}
