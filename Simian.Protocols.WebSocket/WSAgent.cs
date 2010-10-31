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
using System.Net.Sockets;
using System.Text;
using log4net;
using OpenMetaverse;

namespace Simian.Protocols.WebSocket
{
    [System.Diagnostics.DebuggerDisplay("{m_name} {m_id}")]
    public class WSAgent : IScenePresence, IPhysicalPresence
    {
        /// <summary>The number of packet categories to throttle on. If a throttle category is added
        /// or removed, this number must also change</summary>
        const int THROTTLE_CATEGORY_COUNT = 7;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        #region Networking Fields

        /// <summary>SessionID for this client</summary>
        public readonly UUID SessionID;
        /// <summary>Socket this client is connected on</summary>
        public readonly Socket Socket;

        /// <summary>True when this connection is alive, otherwise false</summary>
        public bool IsConnected = true;
        /// <summary>True when this connection is paused, otherwise false</summary>
        public bool IsPaused;
        /// <summary>Environment.TickCount when the last message was received for this client</summary>
        public int TickLastMessageReceived;

        /// <summary>Number of messages received from this client</summary>
        public int MessagesReceived;
        /// <summary>Number of messages sent to this client</summary>
        public int MessagesSent;

        /// <summary>Are we in the process of reading data or not</summary>
        public bool ReadingData;
        /// <summary>True if the last received record is binary</summary>
        public bool ReadingBinary;
        /// <summary>Holds the currently accumulated data</summary>
        public StringBuilder DataString;
        /// <summary>If received data was binary, a binary form is here else null</summary>
        public byte[] DataBinary;

        /// <summary>Holds the Environment.TickCount value of when the next OnQueueEmpty can be fired</summary>
        private int m_nextOnQueueEmpty = 1;

        /// <summary>Throttle bucket for this client</summary>
        private readonly TokenBucket m_throttle;
        /// <summary>Throttle buckets for each message category</summary>
        private readonly TokenBucket[] m_throttleCategories;
        /// <summary>Outgoing queues for throttled messages</summary>
        private readonly LocklessQueue<OutgoingMessage>[] m_messageOutboxes = new LocklessQueue<OutgoingMessage>[THROTTLE_CATEGORY_COUNT];
        /// <summary>A container that can hold one message for each outbox, used to store
        /// dequeued messages that are being held for throttling</summary>
        private readonly OutgoingMessage[] m_nextMessages = new OutgoingMessage[THROTTLE_CATEGORY_COUNT];
        /// <summary>A reference to the server that is managing this client</summary>
        private readonly WebSockets m_server;
        /// <summary>Scene event interest list</summary>
        private readonly InterestList m_interestList;

        #endregion Networking Fields

        #region Avatar Fields

        /// <summary>Agent ID</summary>
        private readonly UUID m_id;
        private readonly uint m_localID;
        /// <summary>Agent name</summary>
        private string m_name = String.Empty;
        private Vector3 m_scale = Vector3.One;
        private Quaternion m_relativeRotation = Quaternion.Identity;
        private Vector4 m_collisionPlane = Vector4.UnitW;
        private ILinkable m_parent;
        private MapAndArray<UUID, ILinkable> m_children = new MapAndArray<UUID, ILinkable>();
        private int m_linkNumber;
        private Quaternion m_lastRotation = Quaternion.Identity;

        /// <summary>Animation tracking</summary>
        private AnimationSet m_animations = new AnimationSet();

        /// <summary>Current animation sequence number</summary>
        public int CurrentAnimSequenceNum;

        public Vector3 CameraPosition;
        public Vector3 CameraAtAxis;
        public Vector3 CameraLeftAxis;
        public Vector3 CameraUpAxis;
        public float DrawDistance = 128.0f;

        public Uri SeedCapability;

        public bool IsRunning;

        //public byte[] VisualParams;
        //public Primitive.TextureEntry TextureEntry;

        #endregion Avatar Fields

        #region Properties

        public UUID ID { get { return m_id; } }
        public uint LocalID { get { return m_localID; } }
        public string Name { get { return m_name; } set { m_name = value; } }
        public UUID OwnerID
        {
            get { return m_id; }
            set { throw new InvalidOperationException("Cannot set the owner of a WSAgent"); }
        }
        public UUID CreatorID
        {
            get { return m_id; }
            set { throw new InvalidOperationException("Cannot set the creator of a WSAgent"); }
        }
        public UUID GroupID { get; set; }
        public Vector3 RelativePosition { get; set; }
        public Quaternion RelativeRotation { get { return m_relativeRotation; } set { m_relativeRotation = value; } }
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
        public Vector3 Scale { get { return m_scale; } set { m_scale = value; } }
        public Vector3 Velocity { get; set; }
        public Vector3 RotationAxis { get; set; }
        public Vector3 AngularVelocity { get; set; }
        public Vector3 Acceleration { get; set; }
        public bool IsVerified { get; set; }
        public bool IsChildPresence { get; set; }
        public IInterestList InterestList { get { return m_interestList; } }
        public float InterestRadius { get { return DrawDistance; } }
        public IScene Scene { get { return m_server.Scene; } }
        public ILinkable Parent { get { return m_parent; } }
        public bool DynamicsEnabled { get; set; }
        public bool CollisionsEnabled { get; set; }
        public bool Frozen { get; set; }
        public Vector3 LastRelativePosition { get; set; }
        public Quaternion LastRelativeRotation { get; set; }
        public Vector3 LastSignificantPosition { get; set; }
        public Vector3 LastAngularVelocity { get; set; }
        public Vector3 LastAcceleration { get; set; }
        public Vector3 LastVelocity { get; set; }
        public Vector3 InputVelocity { get; set; }
        public MovementState MovementState { get; set; }
        public MovementState LastMovementState { get; set; }
        public Vector4 CollisionPlane { get { return m_collisionPlane; } set { m_collisionPlane = value; } }
        public int FallStart  { get; set; }
        public int JumpStart  { get; set; }
        public int StunMS { get; set; }
        public AnimationSet Animations { get { return m_animations; } }

        #endregion Properties

        /// <summary>
        /// Default constructor
        /// </summary>
        public WSAgent(WebSockets server, TokenBucket parentThrottle, ThrottleRates rates,
            UUID agentID, UUID sessionID, Socket socket, bool isChildAgent)
        {
            m_id = agentID;
            m_server = server;
            m_interestList = new InterestList(this, 200);

            IsChildPresence = isChildAgent;

            m_localID = m_server.Scene.CreateLocalID();

            //TextureEntry = new Primitive.TextureEntry(DEFAULT_AVATAR_TEXTURE);

            SessionID = sessionID;
            Socket = socket;

            // Create a token bucket throttle for this client that has the scene token bucket as a parent
            m_throttle = new TokenBucket(parentThrottle, rates.ClientTotalLimit, rates.ClientTotal);
            // Create an array of token buckets for this clients different throttle categories
            m_throttleCategories = new TokenBucket[THROTTLE_CATEGORY_COUNT];

            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
            {
                ThrottleCategory type = (ThrottleCategory)i;

                // Initialize the message outboxes, where messages sit while they are waiting for tokens
                m_messageOutboxes[i] = new LocklessQueue<OutgoingMessage>();
                // Initialize the token buckets that control the throttling for each category
                m_throttleCategories[i] = new TokenBucket(m_throttle, rates.GetLimit(type), rates.GetRate(type));
            }

            // Initialize this to a sane value to prevent early disconnects
            TickLastMessageReceived = Util.TickCount();
        }

        /// <summary>
        /// Shuts down this client connection
        /// </summary>
        public void Shutdown()
        {
            m_log.Info("Shutting down WS agent " + this.Name);

            IsConnected = false;
            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
            {
                m_messageOutboxes[i] = new LocklessQueue<OutgoingMessage>();
                m_nextMessages[i] = null;
            }

            m_server.Scene.EntityRemove(this, this);
        }

        /// <summary>Link number, if this avatar is sitting</summary>
        public int LinkNumber { get { return m_linkNumber; } set { m_linkNumber = value; } }

        public void MarkAsModified()
        {
        }

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
                    //this.RelativePosition = this.RequestedSitOffset;
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

        public BasicMesh GetBasicMesh()
        {
            return null;
        }

        public ConvexHullSet GetConvexHulls()
        {
            return null;
        }

        #endregion IPhysical

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
            resend = Math.Max(resend, WebSocketServer.MTU);
            land = Math.Max(land, WebSocketServer.MTU);
            wind = Math.Max(wind, WebSocketServer.MTU);
            cloud = Math.Max(cloud, WebSocketServer.MTU);
            task = Math.Max(task, WebSocketServer.MTU);
            texture = Math.Max(texture, WebSocketServer.MTU);
            asset = Math.Max(asset, WebSocketServer.MTU);

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

        public bool EnqueueOutgoing(OutgoingMessage message)
        {
            int category = (int)message.Category;

            if (category >= 0 && category < m_messageOutboxes.Length)
            {
                LocklessQueue<OutgoingMessage> queue = m_messageOutboxes[category];
                TokenBucket bucket = m_throttleCategories[category];

                if (bucket.RemoveTokens(message.Data.Length))
                {
                    // Enough tokens were removed from the bucket, the message will not be queued
                    return false;
                }
                else
                {
                    // Not enough tokens in the bucket, queue this message
                    queue.Enqueue(message);
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
        /// Loops through all of the message queues for this client and tries to send
        /// any outgoing messages, obeying the throttling bucket limits
        /// </summary>
        /// <remarks>This function is only called from a synchronous loop in the
        /// server so we don't need to bother making this thread safe</remarks>
        /// <returns>True if any packets were sent, otherwise false</returns>
        public bool DequeueOutgoing()
        {
            OutgoingMessage message;
            LocklessQueue<OutgoingMessage> queue;
            TokenBucket bucket;
            bool messageSent = false;
            ThrottleCategoryFlags emptyCategories = 0;

            for (int i = 0; i < THROTTLE_CATEGORY_COUNT; i++)
            {
                bucket = m_throttleCategories[i];

                if (m_nextMessages[i] != null)
                {
                    // This bucket was empty the last time we tried to send a message,
                    // leaving a dequeued message still waiting to be sent out. Try to
                    // send it again
                    OutgoingMessage nextMessage = m_nextMessages[i];
                    if (bucket.RemoveTokens(nextMessage.Data.Length))
                    {
                        // Send the message
                        //FIXME:m_server.SendMessageFinal(nextMessage);
                        m_nextMessages[i] = null;
                        messageSent = true;
                    }
                }
                else
                {
                    // No dequeued message waiting to be sent, try to pull one off
                    // this queue
                    queue = m_messageOutboxes[i];
                    if (queue.TryDequeue(out message))
                    {
                        // A message was pulled off the queue. See if we have
                        // enough tokens in the bucket to send it out
                        if (bucket.RemoveTokens(message.Data.Length))
                        {
                            // Send the message
                            //FIXME:m_server.SendMessageFinal(message);
                            messageSent = true;
                        }
                        else
                        {
                            // Save the dequeued message for the next iteration
                            m_nextMessages[i] = message;
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

            return messageSent;
        }

        /// <summary>
        /// Does an early check to see if this queue empty callback is already
        /// running, then asynchronously firing the event
        /// </summary>
        /// <param name="throttleIndex">Throttle category to fire the callback
        /// for</param>
        private void BeginFireQueueEmpty(ThrottleCategoryFlags categories)
        {
            if (m_nextOnQueueEmpty != 0 && Util.TickCount() >= m_nextOnQueueEmpty)
            {
                // Use a value of 0 to signal that FireQueueEmpty is running
                m_nextOnQueueEmpty = 0;

                // Asynchronously run the callback
                m_server.Scheduler.FireAndForget(FireQueueEmpty, categories);
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

            int start = Util.TickCount();

            // Dequeue a fixed number of events from the interest list
            m_interestList.DequeueEvents(EVENTS_PER_CALLBACK);

            // Fire the user callback to queue up any other data such as textures
            //FIXME:m_server.FireQueueEmpty(this, categories);

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
            return String.Format("WSAgent \"{0}\" ({1}){2} @ {3}",
                m_name,
                m_id,
                IsChildPresence ? " (ChildAgent)" : String.Empty,
                RelativePosition);
        }
    }
}
