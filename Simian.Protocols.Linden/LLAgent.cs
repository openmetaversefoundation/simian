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
using System.Net;
using log4net;
using OpenMetaverse;

namespace Simian.Protocols.Linden
{
    [System.Diagnostics.DebuggerDisplay("{m_name} {m_id}")]
    public class LLAgent : IScenePresence, IPhysicalPresence
    {
        /// <summary>The number of packet categories to throttle on. If a throttle category is added
        /// or removed, this number must also change</summary>
        const int THROTTLE_CATEGORY_COUNT = 7;

        private static readonly UUID DEFAULT_AVATAR_TEXTURE = new UUID("c228d1cf-4b5d-4ba8-84f4-899a0796aa97");
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        #region Networking Fields

        /// <summary>SessionID for this client</summary>
        public readonly UUID SessionID;
        /// <summary>SecureSessionID for this client</summary>
        public readonly UUID SecureSessionID;        
        /// <summary>Circuit code that this client is connected on</summary>
        public readonly uint CircuitCode;
        /// <summary>Sequence numbers of packets we've received (for duplicate checking)</summary>
        public readonly IncomingPacketHistoryCollection PacketArchive;
        /// <summary>Packets we have sent that need to be ACKed by the client</summary>
        public readonly UnackedPacketCollection NeedAcks;
        /// <summary>ACKs that are queued up, waiting to be sent to the client</summary>
        public readonly LocklessQueue<uint> PendingAcks;

        /// <summary>The remote address of the connected client</summary>
        public IPEndPoint RemoteEndPoint;
        /// <summary>Current packet sequence number</summary>
        public int CurrentSequence;
        /// <summary>Current ping sequence number</summary>
        public byte CurrentPingSequence;
        /// <summary>True when this connection is alive, otherwise false</summary>
        public bool IsConnected;
        /// <summary>True when this connection is paused, otherwise false</summary>
        public bool IsPaused;
        /// <summary>The seat <seealso cref="UUID"/> requested before an agent sends the actual sit message</summary>
        public UUID RequestedSitTarget;
        /// <summary>The seat offset requested before an agent sends the actual sit message</summary>
        public Vector3 RequestedSitOffset;
        /// <summary>Environment.TickCount when the last packet was received for this client</summary>
        public int TickLastPacketReceived;

        /// <summary>Smoothed round-trip time. A smoothed average of the round-trip time for sending a
        /// reliable packet to the client and receiving an ACK</summary>
        public float SRTT;
        /// <summary>Round-trip time variance. Measures the consistency of round-trip times</summary>
        public float RTTVAR;
        /// <summary>Retransmission timeout. Packets that have not been acknowledged in this number of
        /// milliseconds or longer will be resent</summary>
        /// <remarks>Calculated from <seealso cref="SRTT"/> and <seealso cref="RTTVAR"/> using the
        /// guidelines in RFC 2988</remarks>
        public int RTO;
        /// <summary>Number of packets received from this client</summary>
        public int PacketsReceived;
        /// <summary>Number of packets sent to this client</summary>
        public int PacketsSent;
        /// <summary>Total byte count of unacked packets sent to this client</summary>
        public int UnackedBytes;

        /// <summary>Holds the Environment.TickCount value of when the next OnQueueEmpty can be fired</summary>
        private int m_nextOnQueueEmpty;

        /// <summary>Default retransmission timeout, in milliseconds</summary>
        private readonly int m_defaultRTO;
        /// <summary>Maximum retransmission timeout, in milliseconds</summary>
        private readonly int m_maxRTO;
        /// <summary>Throttle bucket for this agent's connection</summary>
        private readonly TokenBucket m_throttle;
        /// <summary>Throttle buckets for each packet category</summary>
        private readonly TokenBucket[] m_throttleCategories;
        /// <summary>Outgoing queues for throttled packets</summary>
        private readonly LocklessQueue<OutgoingPacket>[] m_packetOutboxes;
        /// <summary>A container that can hold one packet for each outbox, used to store
        /// dequeued packets that are being held for throttling</summary>
        private readonly OutgoingPacket[] m_nextPackets;
        /// <summary>A reference to the LLUDPServer that is managing this client</summary>
        private readonly LLUDPServer m_udpServer;
        /// <summary>Scene event interest list</summary>
        private readonly InterestList m_interestList;

        #endregion Networking Fields

        #region Avatar Fields

        /// <summary>Agent ID</summary>
        private readonly UUID m_id;
        /// <summary>Local scene ID</summary>
        private readonly uint m_localID;
        /// <summary>Agent name</summary>
        private string m_name = String.Empty;
        private IScene m_scene;
        private Vector3 m_scale = new Vector3(0.45f, 0.6f, 1.77f); // A reasonable default avatar size
        private Vector3 m_relativePosition;
        private Quaternion m_relativeRotation = Quaternion.Identity;
        private Vector3 m_rotationAxis;
        private Vector3 m_velocity;
        private Vector3 m_acceleration;
        private Vector3 m_angularVelocity;
        private Vector4 m_collisionPlane = Vector4.UnitW;
        private ILinkable m_parent;
        private MapAndArray<UUID, ILinkable> m_children = new MapAndArray<UUID,ILinkable>();
        private int m_linkNumber;
        private bool m_dynamicsEnabled = true;
        private bool m_frozen;
        private bool m_collisionsEnabled = true;
        private Vector3 m_lastPosition;
        private Quaternion m_lastRotation = Quaternion.Identity;
        private Vector3 m_lastAngularVelocity;
        private Vector3 m_lastAcceleration;
        private Vector3 m_lastVelocity;
        private Vector3 m_inputVelocity;
        private MovementState m_movementState;
        private MovementState m_lastMovementState;
        private int m_fallMS;
        private int m_jumpMS;
        private int m_stunMS;
        /// <summary>Animation tracking</summary>
        private AnimationSet m_animations = new AnimationSet();
        private byte m_accessLevel;
        private bool m_isChildPresence;

        /// <summary>Current animation sequence number</summary>
        public int CurrentAnimSequenceNum;

        public int CurrentWearablesSerialNum = -1;

        public Vector3 CameraPosition;
        public Vector3 CameraAtAxis;
        public Vector3 CameraLeftAxis;
        public Vector3 CameraUpAxis;
        public float DrawDistance = 128.0f;

        public Uri SeedCapability;
        public LLEventQueue EventQueue;

        public AgentManager.ControlFlags ControlFlags = AgentManager.ControlFlags.NONE;
        public AgentState State;
        public Vector3 SitPosition;
        public Quaternion SitRotation;
        public bool HideTitle;
        public bool IsRunning;

        public byte[] VisualParams;
        public Primitive.TextureEntry TextureEntry;

        #endregion Avatar Fields

        #region Properties

        public UUID ID { get { return m_id; } }
        public uint LocalID { get { return m_localID; } }

        public string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }
        public UUID OwnerID
        {
            get { return m_id; }
            set { throw new InvalidOperationException("Cannot set the owner of an LLAgent"); }
        }
        public UUID CreatorID
        {
            get { return m_id; }
            set { throw new InvalidOperationException("Cannot set the creator of an LLAgent"); }
        }
        public UUID GroupID
        {
            get { return UUID.Zero; }
            set { }
        }
        public Vector3 RelativePosition
        {
            get { return m_relativePosition; }
            set { m_relativePosition = value; }
        }
        public Quaternion RelativeRotation
        {
            get { return m_relativeRotation; }
            set { m_relativeRotation = value; }
        }
        public Vector3 ScenePosition
        {
            get
            {
                Vector3 position = RelativePosition;

                ILinkable parent = Parent;
                if (parent != null)
                    position = parent.ScenePosition + Vector3.Transform(position, Matrix4.CreateFromQuaternion(parent.SceneRotation));

                return position;
            }
        }
        public Quaternion SceneRotation
        {
            get
            {
                Quaternion rotation = RelativeRotation;

                ILinkable parent = Parent;
                if (parent != null)
                    rotation *= parent.SceneRotation;

                return rotation;
            }
        }
        public Vector3d GlobalPosition
        {
            get
            {
                return Scene.MinPosition + new Vector3d(ScenePosition);
            }
        }
        public AABB SceneAABB
        {
            get
            {
                Vector3 center = ScenePosition;
                Vector3 halfExtent = m_scale * 0.5f;
                return new AABB(center - halfExtent, center + halfExtent);
            }
        }
        public Vector3 Scale
        {
            get { return m_scale; }
            set { m_scale = value; }
        }
        public Vector3 Velocity
        {
            get { return m_velocity; }
            set { m_velocity = value; }
        }
        public Vector3 RotationAxis
        {
            get { return m_rotationAxis; }
            set { m_rotationAxis = value; }
        }
        public Vector3 AngularVelocity
        {
            get { return m_angularVelocity; }
            set { m_angularVelocity = value; }
        }
        public Vector3 Acceleration
        {
            get { return m_acceleration; }
            set { m_acceleration = value; }
        }
        public bool IsVerified
        {
            get { return m_accessLevel > 0; }
            set
            {
                if (!value) m_accessLevel = 0;
                else if (m_accessLevel == 0) m_accessLevel = 1;
            }
        }
        public byte AccessLevel
        {
            get { return m_accessLevel; }
            set { m_accessLevel = value; }
        }
        public bool IsChildPresence
        {
            get { return m_isChildPresence; }
            set { m_isChildPresence = value; }
        }
        public Vector4 CollisionPlane
        {
            get { return m_collisionPlane; }
            set { m_collisionPlane = value; }
        }
        public IInterestList InterestList { get { return m_interestList; } }
        public float InterestRadius { get { return DrawDistance; } }
        public IScene Scene { get { return m_scene; } }
        public ILinkable Parent
        {
            get { return m_parent; }
        }
        public bool DynamicsEnabled
        {
            get { return m_dynamicsEnabled && m_parent == null; }
            set { m_dynamicsEnabled = value; }
        }
        public bool CollisionsEnabled
        {
            get { return m_collisionsEnabled; }
            set { m_collisionsEnabled = value; }
        }
        public bool Frozen
        {
            get { return m_frozen; }
            set { m_frozen = value; }
        }
        public Vector3 LastRelativePosition
        {
            get { return m_lastPosition; }
            set { m_lastPosition = value; }
        }
        public Quaternion LastRelativeRotation
        {
            get { return m_lastRotation; }
            set { m_lastRotation = value; }
        }
        public Vector3 LastSignificantPosition { get; set; }
        public Vector3 LastAngularVelocity
        {
            get { return m_lastAngularVelocity; }
            set { m_lastAngularVelocity = value; }
        }
        public Vector3 LastAcceleration
        {
            get { return m_lastAcceleration; }
            set { m_lastAcceleration = value; }
        }
        public Vector3 LastVelocity
        {
            get { return m_lastVelocity; }
            set { m_lastVelocity = value; }
        }
        public Vector3 InputVelocity
        {
            get { return m_inputVelocity; }
            set { m_inputVelocity = value; }
        }
        public MovementState MovementState
        {
            get { return m_movementState; }
            set { m_movementState = value; }
        }
        public MovementState LastMovementState
        {
            get { return m_lastMovementState; }
            set { m_lastMovementState = value; }
        }
        public int FallStart
        {
            get { return m_fallMS; }
            set { m_fallMS = value; }
        }
        public int JumpStart
        {
            get { return m_jumpMS; }
            set { m_jumpMS = value; }
        }
        public int StunMS
        {
            get { return m_stunMS; }
            set { m_stunMS = value; }
        }
        public AnimationSet Animations { get { return m_animations; } }

        #endregion Properties

        #region Creation/Teardown

        /// <summary>
        /// Create a non-networked LLAgent
        /// </summary>
        /// <param name="name">Agent name</param>
        /// <param name="agentID">AgentID for this agent</param>
        /// <param name="sessionID">SessionID for this agent</param>
        /// <param name="secureSessionID">SecureSessionID for this agent</param>
        /// <param name="scene">Scene this agent exists in</param>
        /// <param name="isChildAgent">True if this agent is currently simulated by
        /// another simulator, otherwise false</param>
        public LLAgent(string name, UUID agentID, UUID sessionID, UUID secureSessionID, IScene scene, bool isChildAgent)
        {
            m_name = name;
            m_id = agentID;
            m_localID = scene.CreateLocalID();
            m_scene = scene;

            SessionID = sessionID;
            SecureSessionID = secureSessionID;

            TextureEntry = new Primitive.TextureEntry(DEFAULT_AVATAR_TEXTURE);

            IsChildPresence = isChildAgent;
            IsConnected = true;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="server">Reference to the UDP server this client is connected to</param>
        /// <param name="rates">Default throttling rates and maximum throttle limits</param>
        /// <param name="parentThrottle">Parent HTB (hierarchical token bucket)
        /// that the child throttles will be governed by</param>
        /// <param name="circuitCode">Circuit code for this connection</param>
        /// <param name="agentID">AgentID for the connected agent</param>
        /// <param name="sessionID">SessionID for the connected agent</param>
        /// <param name="secureSessionID">SecureSessionID for the connected agent</param>
        /// <param name="defaultRTO">Default retransmission timeout, in milliseconds</param>
        /// <param name="maxRTO">Maximum retransmission timeout, in milliseconds</param>
        /// <param name="remoteEndPoint">Remote endpoint for this connection</param>
        /// <param name="isChildAgent">True if this agent is currently simulated by
        /// another simulator, otherwise false</param>
        public LLAgent(LLUDPServer server, ThrottleRates rates, TokenBucket parentThrottle,
            uint circuitCode, UUID agentID, UUID sessionID, UUID secureSessionID, IPEndPoint remoteEndPoint,
            int defaultRTO, int maxRTO, bool isChildAgent)
        {
            m_id = agentID;
            m_udpServer = server;
            m_scene = m_udpServer.Scene;

            PacketArchive = new IncomingPacketHistoryCollection(200);
            NeedAcks = new UnackedPacketCollection();
            PendingAcks = new LocklessQueue<uint>();
            EventQueue = new LLEventQueue();

            m_nextOnQueueEmpty = 1;
            m_defaultRTO = 1000 * 3;
            m_maxRTO = 1000 * 60;

            m_packetOutboxes = new LocklessQueue<OutgoingPacket>[THROTTLE_CATEGORY_COUNT];
            m_nextPackets = new OutgoingPacket[THROTTLE_CATEGORY_COUNT];
            m_interestList = new InterestList(this, 200);

            IsChildPresence = isChildAgent;

            m_localID = m_scene.CreateLocalID();

            TextureEntry = new Primitive.TextureEntry(DEFAULT_AVATAR_TEXTURE);

            SessionID = sessionID;
            SecureSessionID = secureSessionID;
            RemoteEndPoint = remoteEndPoint;
            CircuitCode = circuitCode;
            
            if (defaultRTO != 0)
                m_defaultRTO = defaultRTO;
            if (maxRTO != 0)
                m_maxRTO = maxRTO;

            // Create a token bucket throttle for this client that has the scene token bucket as a parent
            m_throttle = new TokenBucket(parentThrottle, rates.ClientTotalLimit, rates.ClientTotal);
            // Create an array of token buckets for this clients different throttle categories
            m_throttleCategories = new TokenBucket[THROTTLE_CATEGORY_COUNT];

            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
            {
                ThrottleCategory type = (ThrottleCategory)i;

                // Initialize the packet outboxes, where packets sit while they are waiting for tokens
                m_packetOutboxes[i] = new LocklessQueue<OutgoingPacket>();
                // Initialize the token buckets that control the throttling for each category
                m_throttleCategories[i] = new TokenBucket(m_throttle, rates.GetLimit(type), rates.GetRate(type));
            }

            // Default the retransmission timeout to three seconds
            RTO = m_defaultRTO;

            // Initialize this to a sane value to prevent early disconnects
            TickLastPacketReceived = Environment.TickCount & Int32.MaxValue;

            IsConnected = true;
        }

        /// <summary>
        /// Shuts down this client connection
        /// </summary>
        public void Shutdown()
        {
            m_log.Info("Shutting down agent " + this.Name);

            IsConnected = false;
            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
            {
                m_packetOutboxes[i] = new LocklessQueue<OutgoingPacket>();
                m_nextPackets[i] = null;
            }

            EventQueue.Dispose();

            m_scene.EntityRemove(this, this);
        }

        #endregion Creation/Teardown

        /// <summary>Link number, if this avatar is sitting</summary>
        public int LinkNumber { get { return m_linkNumber; } set { m_linkNumber = value; } }

        #region ILinkable

        public ILinkable[] GetChildren()
        {
            return m_children.GetArray();
        }

        public void AddChild(ILinkable child)
        {
            child.SetParent(this, true, true);
        }

        public void SetParent(ILinkable parent, bool adjustPosRot, bool sendUpdate)
        {
            // If this is already the parent then nevermind
            if (parent == m_parent)
                return;

            // Delink from old parent if we are already linked
            if (m_parent != null)
            {
                // Transform orientation back from local to scene orientation
                this.RelativePosition = m_parent.RelativePosition + Vector3.Transform(this.RelativePosition,
                    Matrix4.CreateFromQuaternion(m_parent.RelativeRotation));
                this.RelativeRotation *= m_parent.RelativeRotation;

                // Remove us from the old parent
                m_parent.RemoveChild(this.ID);
            }

            // Link ourself to the new parent
            if (parent != null)
            {
                // Do not move children (attachments) to new parent
                //ILinkable[] children = GetChildren();
                //for (int c = 0; c < children.Length; c++)
                //    children[c].SetParent(parent, adjustPosRot);

                // Assign new parent
                m_parent = parent;

                if (adjustPosRot)
                {
                    // Transform from scene orientation to new local orientation
                    this.RelativePosition = Vector3.Transform(this.RelativePosition - m_parent.RelativePosition,
                        Matrix4.CreateFromQuaternion(Quaternion.Identity / m_parent.RelativeRotation));
                    this.RelativeRotation /= m_parent.RelativeRotation;
                }
                else
                {
                    //TODO: sittarget
                    this.RelativePosition = this.RequestedSitOffset;
                    this.RelativeRotation = Quaternion.Identity;
                }                

                // Add us as a child of the new parent
                m_parent.AddChild(this);
                //Avatar.ParentID = m_parent.LocalID; //TODO
            }
            else
            {
                // No new parent (not linked)
                m_parent = null;
                //Avatar.ParentID = 0; //TODO
            }

            if (sendUpdate)
                Scene.EntityAddOrUpdate(this, this, UpdateFlags.Position | UpdateFlags.Rotation | UpdateFlags.Parent, 0);
        }

        public bool RemoveChild(UUID childID)
        {
            ILinkable child;
            if (m_children.TryGetValue(childID, out child))
            {
                child.SetParent(null, true, true);
                return true;
            }
            return false;
        }

        #endregion ILinkable

        #region IPhysical

        public float GetMass()
        {
            const float AVATAR_DENSITY = 400f; //kg/m^3
            return (float)(Math.PI * Math.Pow(m_scale.X, 2) * m_scale.Z * AVATAR_DENSITY);
        }

        public void ResetMass()
        {
        }

        public PhysicsType GetPhysicsType()
        {
            return PhysicsType.Avatar;
        }

        public ulong GetPhysicsKey()
        {
            return 0;
        }

        public PhysicsHull GetPhysicsHull()
        {
            return null;
        }

        #endregion IPhysical

        public void UpdateHeight()
        {
            byte[] visualParams = VisualParams;
            //float agentSizeVPHeight, agentSizeVPHeelHeight, agentSizeVPPlatformHeight, agentSizeVPHeadSize,
            //    agentSizeVPLegLength, agentSizeVPNeckLength, agentSizeVPHipLength;

            if (visualParams != null && visualParams.Length == 218)
            {
                // FIXME: Need to figure out how to convert from OpenMetaverse.VisualParams.Params indices to a VisualParams[218] index
                /*
                VisualParam vp;

                vp = OpenMetaverse.VisualParams.Params[33];
                agentSizeVPHeight = Utils.ByteToFloat(visualParams[vp.ParamID], vp.MinValue, vp.MaxValue);

                vp = OpenMetaverse.VisualParams.Params[198];
                agentSizeVPHeelHeight = Utils.ByteToFloat(visualParams[vp.ParamID], vp.MinValue, vp.MaxValue);

                vp = OpenMetaverse.VisualParams.Params[503];
                agentSizeVPPlatformHeight = Utils.ByteToFloat(visualParams[vp.ParamID], vp.MinValue, vp.MaxValue);

                vp = OpenMetaverse.VisualParams.Params[682];
                agentSizeVPHeadSize = Utils.ByteToFloat(visualParams[vp.ParamID], vp.MinValue, vp.MaxValue);

                vp = OpenMetaverse.VisualParams.Params[692];
                agentSizeVPLegLength = Utils.ByteToFloat(visualParams[vp.ParamID], vp.MinValue, vp.MaxValue);

                vp = OpenMetaverse.VisualParams.Params[756];
                agentSizeVPNeckLength = Utils.ByteToFloat(visualParams[vp.ParamID], vp.MinValue, vp.MaxValue);

                vp = OpenMetaverse.VisualParams.Params[842];
                agentSizeVPHipLength = Utils.ByteToFloat(visualParams[vp.ParamID], vp.MinValue, vp.MaxValue);

                // Takes into account the Shoe Heel/Platform offsets but not the HeadSize offset. Seems to work.
                double agentSizeBase = 1.706;

                // The calculation for the HeadSize scalar may be incorrect, but it seems to work
                double agentHeight = agentSizeBase + (agentSizeVPLegLength * .1918) + (agentSizeVPHipLength * .0375) +
                    (agentSizeVPHeight * .12022) + (agentSizeVPHeadSize * .01117) + (agentSizeVPNeckLength * .038) +
                    (agentSizeVPHeelHeight * .08) + (agentSizeVPPlatformHeight * .07);

                Vector3 newScale = new Vector3(0.45f, 0.6f, (float)agentHeight);

                m_log.Debug("Changing scale for " + Name + " from " + Scale + " to " + newScale);

                Scale = newScale;
                */
            }
        }

        #region Networking

        public void SetThrottles(byte[] throttleData)
        {
            byte[] adjData;
            int pos = 0;

            if (!BitConverter.IsLittleEndian)
            {
                byte[] newData = new byte[7 * 4];
                Buffer.BlockCopy(throttleData, 0, newData, 0, 7 * 4);

                for (int i = 0; i < 7; i++)
                    Array.Reverse(newData, i * 4, 4);

                adjData = newData;
            }
            else
            {
                adjData = throttleData;
            }

            // 0.125f converts from bits to bytes
            int resend = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int land = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int wind = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int cloud = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int task = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int texture = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f); pos += 4;
            int asset = (int)(BitConverter.ToSingle(adjData, pos) * 0.125f);

            // Make sure none of the throttles are set below our packet MTU,
            // otherwise a throttle could become permanently clogged
            resend = Math.Max(resend, LLUDPServer.MTU);
            land = Math.Max(land, LLUDPServer.MTU);
            wind = Math.Max(wind, LLUDPServer.MTU);
            cloud = Math.Max(cloud, LLUDPServer.MTU);
            task = Math.Max(task, LLUDPServer.MTU);
            texture = Math.Max(texture, LLUDPServer.MTU);
            asset = Math.Max(asset, LLUDPServer.MTU);

            int total = resend + land + wind + cloud + task + texture + asset;

            //m_log.DebugFormat("{0} is setting throttles. Resend={1}, Land={2}, Wind={3}, Cloud={4}, Task={5}, Texture={6}, Asset={7}, Total={8}",
            //    AgentID, resend, land, wind, cloud, task, texture, asset, total);

            // Update the token buckets with new throttle values
            TokenBucket bucket;

            bucket = m_throttle;
            bucket.MaxBurst = total;

            bucket = m_throttleCategories[(int)ThrottleCategory.Resend];
            bucket.DripRate = resend;
            bucket.MaxBurst = resend;

            bucket = m_throttleCategories[(int)ThrottleCategory.Land];
            bucket.DripRate = land;
            bucket.MaxBurst = land;

            bucket = m_throttleCategories[(int)ThrottleCategory.Wind];
            bucket.DripRate = wind;
            bucket.MaxBurst = wind;

            bucket = m_throttleCategories[(int)ThrottleCategory.Cloud];
            bucket.DripRate = cloud;
            bucket.MaxBurst = cloud;

            bucket = m_throttleCategories[(int)ThrottleCategory.Asset];
            bucket.DripRate = asset;
            bucket.MaxBurst = asset;

            bucket = m_throttleCategories[(int)ThrottleCategory.Task];
            bucket.DripRate = task;
            bucket.MaxBurst = task;

            bucket = m_throttleCategories[(int)ThrottleCategory.Texture];
            bucket.DripRate = texture;
            bucket.MaxBurst = texture;
        }

        public byte[] GetThrottlesPacked()
        {
            byte[] data = new byte[7 * 4];
            int i = 0;

            Buffer.BlockCopy(Utils.FloatToBytes((float)m_throttleCategories[(int)ThrottleCategory.Resend].DripRate), 0, data, i, 4); i += 4;
            Buffer.BlockCopy(Utils.FloatToBytes((float)m_throttleCategories[(int)ThrottleCategory.Land].DripRate), 0, data, i, 4); i += 4;
            Buffer.BlockCopy(Utils.FloatToBytes((float)m_throttleCategories[(int)ThrottleCategory.Wind].DripRate), 0, data, i, 4); i += 4;
            Buffer.BlockCopy(Utils.FloatToBytes((float)m_throttleCategories[(int)ThrottleCategory.Cloud].DripRate), 0, data, i, 4); i += 4;
            Buffer.BlockCopy(Utils.FloatToBytes((float)m_throttleCategories[(int)ThrottleCategory.Task].DripRate), 0, data, i, 4); i += 4;
            Buffer.BlockCopy(Utils.FloatToBytes((float)m_throttleCategories[(int)ThrottleCategory.Texture].DripRate), 0, data, i, 4); i += 4;
            Buffer.BlockCopy(Utils.FloatToBytes((float)m_throttleCategories[(int)ThrottleCategory.Asset].DripRate), 0, data, i, 4); i += 4;

            return data;
        }

        public bool EnqueueOutgoing(OutgoingPacket packet)
        {
            int category = (int)packet.Category;

            if (category >= 0 && category < m_packetOutboxes.Length)
            {
                LocklessQueue<OutgoingPacket> queue = m_packetOutboxes[category];
                TokenBucket bucket = m_throttleCategories[category];

                if (bucket.RemoveTokens(packet.Buffer.DataLength))
                {
                    // Enough tokens were removed from the bucket, the packet will not be queued
                    return false;
                }
                else
                {
                    // Not enough tokens in the bucket, queue this packet
                    queue.Enqueue(packet);
                    return true;
                }
            }
            else
            {
                // We don't have a token bucket for this category, so it will not be queued
                return false;
            }
        }

        /// <summary>
        /// Loops through all of the packet queues for this client and tries to send
        /// any outgoing packets, obeying the throttling bucket limits
        /// </summary>
        /// <remarks>This function is only called from a synchronous loop in the
        /// UDPServer so we don't need to bother making this thread safe</remarks>
        /// <returns>True if any packets were sent, otherwise false</returns>
        public bool DequeueOutgoing()
        {
            OutgoingPacket packet;
            LocklessQueue<OutgoingPacket> queue;
            TokenBucket bucket;
            bool packetSent = false;
            ThrottleCategoryFlags emptyCategories = 0;

            //string queueDebugOutput = String.Empty; // Serious debug business

            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
            {
                bucket = m_throttleCategories[i];
                //queueDebugOutput += m_packetOutboxes[i].Count + " ";  // Serious debug business

                if (m_nextPackets[i] != null)
                {
                    // This bucket was empty the last time we tried to send a packet,
                    // leaving a dequeued packet still waiting to be sent out. Try to
                    // send it again
                    OutgoingPacket nextPacket = m_nextPackets[i];
                    if (bucket.RemoveTokens(nextPacket.Buffer.DataLength))
                    {
                        // Send the packet
                        m_udpServer.SendPacketFinal(nextPacket);
                        m_nextPackets[i] = null;
                        packetSent = true;
                    }
                }
                else
                {
                    // No dequeued packet waiting to be sent, try to pull one off
                    // this queue
                    queue = m_packetOutboxes[i];
                    if (queue.TryDequeue(out packet))
                    {
                        // A packet was pulled off the queue. See if we have
                        // enough tokens in the bucket to send it out
                        if (bucket.RemoveTokens(packet.Buffer.DataLength))
                        {
                            // Send the packet
                            m_udpServer.SendPacketFinal(packet);
                            packetSent = true;
                        }
                        else
                        {
                            // Save the dequeued packet for the next iteration
                            m_nextPackets[i] = packet;
                        }

                        // If the queue is empty after this dequeue, fire the queue
                        // empty callback now so it has a chance to fill before we 
                        // get back here
                        if (queue.Count == 0)
                            emptyCategories |= CategoryToFlag(i);
                    }
                    else
                    {
                        // No packets in this queue. Fire the queue empty callback
                        // if it has not been called recently
                        emptyCategories |= CategoryToFlag(i);
                    }
                }
            }

            if (emptyCategories != 0)
                BeginFireQueueEmpty(emptyCategories);

            //m_log.Info("[LLUDPCLIENT]: Queues: " + queueDebugOutput); // Serious debug business
            return packetSent;
        }

        /// <summary>
        /// Called when an ACK packet is received and a round-trip time for a
        /// packet is calculated. This is used to calculate the smoothed
        /// round-trip time, round trip time variance, and finally the
        /// retransmission timeout
        /// </summary>
        /// <param name="r">Round-trip time of a single packet and its
        /// acknowledgement</param>
        public void UpdateRoundTrip(float r)
        {
            const float ALPHA = 0.125f;
            const float BETA = 0.25f;
            const float K = 4.0f;

            if (RTTVAR == 0.0f)
            {
                // First RTT measurement
                SRTT = r;
                RTTVAR = r * 0.5f;
            }
            else
            {
                // Subsequence RTT measurement
                RTTVAR = (1.0f - BETA) * RTTVAR + BETA * Math.Abs(SRTT - r);
                SRTT = (1.0f - ALPHA) * SRTT + ALPHA * r;
            }

            int rto = (int)(SRTT + Math.Max(Scene.Simian.TickCountResolution, K * RTTVAR));

            // Clamp the retransmission timeout to manageable values
            rto = Utils.Clamp(RTO, m_defaultRTO, m_maxRTO);

            RTO = rto;

            //m_log.Debug("[LLUDPCLIENT]: Setting agent " + this.Agent.FullName + "'s RTO to " + RTO + "ms with an RTTVAR of " +
            //    RTTVAR + " based on new RTT of " + r + "ms");
        }

        /// <summary>
        /// Exponential backoff of the retransmission timeout, per section 5.5
        /// of RFC 2988
        /// </summary>
        public void BackoffRTO()
        {
            // Reset SRTT and RTTVAR, we assume they are bogus since things
            // didn't work out and we're backing off the timeout
            SRTT = 0.0f;
            RTTVAR = 0.0f;

            // Double the retransmission timeout
            RTO = Math.Min(RTO * 2, m_maxRTO);
        }

        /// <summary>
        /// Does an early check to see if this queue empty callback is already
        /// running, then asynchronously firing the event
        /// </summary>
        /// <param name="throttleIndex">Throttle category to fire the callback
        /// for</param>
        private void BeginFireQueueEmpty(ThrottleCategoryFlags categories)
        {
            if (m_nextOnQueueEmpty != 0 && (Environment.TickCount & Int32.MaxValue) >= m_nextOnQueueEmpty)
            {
                // Use a value of 0 to signal that FireQueueEmpty is running
                m_nextOnQueueEmpty = 0;

                // Asynchronously run the callback
                m_udpServer.Scheduler.FireAndForget(FireQueueEmpty, categories);
            }
        }

        /// <summary>
        /// Processes queued data for this agent and sets the minimum time that
        /// this method can be called again
        /// </summary>
        /// <param name="o">Throttle categories that are empty, stored as an 
        /// object to match the WaitCallback delegate signature</param>
        private void FireQueueEmpty(object o)
        {
            const int MIN_CALLBACK_MS = 30;
            const int EVENTS_PER_CALLBACK = 50;

            ThrottleCategoryFlags categories = (ThrottleCategoryFlags)o;
            
            int start = Environment.TickCount & Int32.MaxValue;

            // Dequeue a fixed number of events from the interest list
            m_interestList.DequeueEvents(EVENTS_PER_CALLBACK);

            // Fire the user callback to queue up any other data such as textures
            m_udpServer.FireQueueEmpty(this, categories);

            m_nextOnQueueEmpty = start + MIN_CALLBACK_MS;
            System.Threading.Interlocked.CompareExchange(ref m_nextOnQueueEmpty, 1, 0);
        }

        /// <summary>
        /// Converts a <seealso cref="ThrottleCategory"/> integer to a
        /// flag value
        /// </summary>
        /// <param name="i">Throttle category to convert</param>
        /// <returns>Flag representation of the throttle category</returns>
        private static ThrottleCategoryFlags CategoryToFlag(int i)
        {
            ThrottleCategory category = (ThrottleCategory)i;

            switch (category)
            {
                case ThrottleCategory.Land:
                    return ThrottleCategoryFlags.Land;
                case ThrottleCategory.Wind:
                    return ThrottleCategoryFlags.Wind;
                case ThrottleCategory.Cloud:
                    return ThrottleCategoryFlags.Cloud;
                case ThrottleCategory.Task:
                    return ThrottleCategoryFlags.Task;
                case ThrottleCategory.Texture:
                    return ThrottleCategoryFlags.Texture;
                case ThrottleCategory.Asset:
                    return ThrottleCategoryFlags.Asset;
                default:
                    return 0;
            }
        }

        #endregion Networking

        public override string ToString()
        {
            return String.Format("LLAgent \"{0}\" ({1}){2} @ {3}",
                m_name,
                m_id,
                IsChildPresence ? " (ChildAgent)" : String.Empty,
                m_relativePosition);
        }
    }
}
