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
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.WebSocket
{
    public delegate void WebSocketClientConnectedEventHandler(WSAgent agent);
    public delegate void WebSocketClientDisconnectedEventHandler(WSAgent agent);
    public delegate void WebSocketDataReceivedEventHandler(WSAgent agent, string data);

    public sealed class WebSocketServer
    {
        /// <summary>Maximum transmission unit, or maximum length of an 
        /// outgoing message</summary>
        public const int MTU = 1400;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Triggered when a client connects</summary>
        public event WebSocketClientConnectedEventHandler Connected;
        /// <summary>Triggered when a client disconnectes or is disconnected</summary>
        /// <remarks>If this is not handled properly, it may burn the hardware 
        /// jolts and cause kernel panic</remarks>
        public event WebSocketClientDisconnectedEventHandler Disconnected;
        /// <summary>Triggered when a message has been received from a client</summary>
        public event WebSocketDataReceivedEventHandler DataReceived;

        /// <summary>High-level server object that manages this low-level
        /// server</summary>
        private readonly WebSockets m_server;
        /// <summary>Bandwidth throttle for this UDP server</summary>
        private readonly TokenBucket m_throttle;
        /// <summary>Bandwidth throttle rates for this UDP server</summary>
        private readonly ThrottleRates m_throttleRates;

        /// <summary>The socket used to listen for incoming connection requests</summary>
        private Socket m_listenSocket;
        /// <summary>Pool of reusable SocketAsyncEventArgs objects for read and accept socket operations</summary>
        private ObjectPool<SocketAsyncEventArgs> m_readPool;
        /// <summary>Buffer size to use for receiving data</summary>
        private int m_receiveBufferSize;
        private MapsAndArray<UUID, IPEndPoint, WSAgent> m_clients = new MapsAndArray<UUID, IPEndPoint, WSAgent>();
        private string m_webSocketOrigin;
        private string m_webSocketLocation;

        public WebSocketServer(WebSockets server)
        {
            m_server = server;

            m_receiveBufferSize = MTU;
            m_readPool = new ObjectPool<SocketAsyncEventArgs>(0, CreateSocketArgs);

            // TODO: Support throttle config
            m_throttleRates = new ThrottleRates(MTU, null);
            m_throttle = new TokenBucket(null, m_throttleRates.SceneTotalLimit, m_throttleRates.SceneTotal);
        }

        /// <summary>
        /// Start listening for incoming connections on the given port and 
        /// default interface
        /// </summary>
        /// <param name="port">Port number to bind this server to</param>
        /// <param name="origin">Origin from which the server is willing to 
        /// accept connections, usually this is your web server. For example: 
        /// http://localhost:8080</param>
        /// <param name="location">Location of the web socket server. For example: ws://localhost:8080/service</param>
        public void Start(int port, string origin, string location)
        {
            // Get the endpoint for the listener
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, port);
            Start(localEndPoint, origin, location);
        }

        /// <summary>
        /// Start listening for incoming connections on the given endpoint
        /// </summary>
        /// <param name="localEndPoint">Network address to bind this server to</param>
        /// <param name="origin">Origin from which the server is willing to 
        /// accept connections, usually this is your web server. For example: 
        /// http://localhost:8080</param>
        /// <param name="location">Location of the web socket server. For example: ws://localhost:8080/service</param>
        public void Start(IPEndPoint localEndPoint, string origin, string location)
        {
            m_webSocketOrigin = origin;
            m_webSocketLocation = location;

            // Create the socket which listens for incoming connections
            m_listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            if (localEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // Set dual-mode (IPv4 & IPv6) for the socket listener
                // 27 is equivalent to IPV6_V6ONLY socket option in the winsock snippet below,
                // based on http://blogs.msdn.com/wndp/archive/2006/10/24/creating-ip-agnostic-applications-part-2-dual-mode-sockets.aspx
                m_listenSocket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);
                m_listenSocket.Bind(new IPEndPoint(IPAddress.IPv6Any, localEndPoint.Port));
            }
            else
            {
                // Associate the socket with the local endpoint
                m_listenSocket.Bind(localEndPoint);
            }

            // Start the server with a listen backlog of 100 connections
            m_listenSocket.Listen(100);

            m_log.Info("WebSocket server listening at " + m_listenSocket.LocalEndPoint);

            // Start accepting connections on the listening socket
            StartAccept(null);
        }

        /// <summary>
        /// Stop the server
        /// </summary>
        public void Stop()
        {
            if (m_listenSocket != null)
            {
                m_listenSocket.Close();
                m_listenSocket = null;
            }
        }

        public void SendMessage(WSAgent agent, OSDMap message, ThrottleCategory category)
        {
            byte[] messageData = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(message));
            byte[] data = new byte[messageData.Length + 2];

            // Start with 0x00
            data[0] = 0x00;
            // Then the string
            Buffer.BlockCopy(messageData, 0, data, 1, messageData.Length);
            // End with 0xFF
            data[data.Length - 1] = 0xFF;

            SendMessageData(agent, data, category);
        }

        public void BroadcastMessage(OSDMap message, ThrottleCategory category)
        {
            byte[] messageData = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(message));
            byte[] data = new byte[messageData.Length + 2];

            // Start with 0x00
            data[0] = 0x00;
            // Then the string
            Buffer.BlockCopy(messageData, 0, data, 1, messageData.Length);
            // End with 0xFF
            data[data.Length - 1] = 0xFF;

            m_clients.ForEach(delegate(WSAgent agent) { SendMessageData(agent, data, category); });
        }

        private void SendMessageData(WSAgent agent, byte[] data, ThrottleCategory category)
        {
            // TODO: Throttling
            SendMessageFinal(agent.Socket, data);
        }

        private void SendMessageFinal(Socket destination, byte[] data)
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.Completed += OnIOCompleted;
            args.SetBuffer(data, 0, data.Length);
            args.RemoteEndPoint = destination.RemoteEndPoint;

            try
            {
                if (!destination.SendAsync(args))
                    ProcessSend(args);
            }
            catch (Exception ex)
            {
                m_log.Warn("Failed to send data to " + destination.RemoteEndPoint + ": " + ex.Message);
            }
        }

        #region Socket Handling

        /// <summary>
        /// Begins an operation to accept a connection request from the client
        /// </summary>
        /// <param name="acceptEventArg">The context object to use when issuing 
        /// the accept operation on the server's listening socket</param>
        private void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                // Only read/write SocketAsyncEventArgs are pooled
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += ProcessAccept;
            }
            else
            {
                // Socket must be cleared since the context object is being reused
                acceptEventArg.AcceptSocket = null;
            }

            if (m_listenSocket != null && !m_listenSocket.AcceptAsync(acceptEventArg))
                ProcessAccept(this, acceptEventArg);
        }

        /// <summary>
        /// Process the accept for the socket listener
        /// </summary>
        /// <param name="sender">Object who raised the event</param>
        /// <param name="e">SocketAsyncEventArg associated with the completed 
        /// accept operation</param>
        private void ProcessAccept(object sender, SocketAsyncEventArgs e)
        {
            //m_log.Debug("ProcessAccept() from " + e.AcceptSocket.RemoteEndPoint);

            // Get the socket for the accepted client connection and put it into the 
            // ReadEventArg object user token
            SocketAsyncEventArgs readEventArgs = m_readPool.Pop();
            readEventArgs.UserToken = e.AcceptSocket;

            // As soon as the client is connected, post a receive to the connection
            if (!e.AcceptSocket.ReceiveAsync(readEventArgs))
                ProcessReceive(readEventArgs);

            // Accept the next connection request
            StartAccept(e);
        }

        /// <summary>
        /// This method is invoked when an asynchronous receive operation 
        /// completes. If the remote host closed the connection, then the 
        /// socket is closed. If data was received then the data is echoed back
        /// to the client
        /// </summary>
        /// <param name="e">SocketAsyncEventArg associated with the completed 
        /// receive operation</param>
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            //m_log.Debug("ProcessReceive(): BytesTransferred=" + e.BytesTransferred);

            Socket s = (Socket)e.UserToken;
            IPEndPoint remoteEndPoint = (IPEndPoint)s.RemoteEndPoint;
            WSAgent agent;

            // Check if the remote host closed the connection
            if (e.BytesTransferred > 0)
            {
                if (e.SocketError == SocketError.Success)
                {
                    m_clients.TryGetValue(remoteEndPoint, out agent);

                    // Process the received data
                    string message = HandleWebSocketReceive(agent, s, remoteEndPoint, e);
                    if (message != null)
                    {
                        // Trigger the data received event
                        WebSocketDataReceivedEventHandler handler = DataReceived;
                        if (handler != null)
                            handler(agent, message);
                    }

                    // Read the next block of data send from the client
                    if (!s.ReceiveAsync(e))
                        ProcessReceive(e);

                    // Free the SocketAsyncEventArg so it can be reused by another client
                    m_readPool.Push(e);
                }
                else
                {
                    CloseClientSocket(e);
                }
            }
            else
            {
                if (m_clients.TryGetValue(remoteEndPoint, out agent))
                {
                    WebSocketClientDisconnectedEventHandler handler = Disconnected;
                    if (handler != null)
                        handler(agent);
                }
            }
        }

        /// <summary>
        /// This method is invoked when an asynchronous send operation 
        /// completes
        /// </summary>
        /// <param name="e">SocketAsyncEventArg associated with the completed 
        /// send operation</param>
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            //m_log.Debug("ProcessSend(): SocketError=" + e.SocketError + ", BytesTransferred=" + e.BytesTransferred);

            if (e.SocketError == SocketError.Success)
            {
                Socket s = (Socket)e.UserToken;
            }
            else
            {
                CloseClientSocket(e);
            }
        }

        /// <summary>
        /// Callback called whenever a receive or send operation is completed 
        /// on a socket
        /// </summary>
        /// <param name="sender">Object who raised the event</param>
        /// <param name="e">SocketAsyncEventArg associated with the completed 
        /// send/receive operation</param>
        private void OnIOCompleted(object sender, SocketAsyncEventArgs e)
        {
            //m_log.Debug("OnIOCompleted(): LastOperation=" + e.LastOperation);

            // Determine which type of operation just completed and call the associated handler
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        /// <summary>
        /// Close the socket associated with the client
        /// </summary>
        /// <param name="e">SocketAsyncEventArg associated with the completed 
        /// send/receive operation</param>
        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            Socket s = (Socket)e.UserToken;

            m_log.Info("Shutting down client connection from " + s.RemoteEndPoint);

            // Throws if client process has already closed
            try
            {
                s.Shutdown(SocketShutdown.Send);
                s.Close();
            }
            catch (Exception) { }
        }

        #endregion Socket Handling

        #region Web Socket Handling

        private byte[] BuildHandshake()
        {
            using (MemoryStream memory = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(memory))
                {
                    writer.Write("HTTP/1.1 101 Web Socket Protocol Handshake\r\n");
                    writer.Write("Upgrade: WebSocket\r\n");
                    writer.Write("Connection: Upgrade\r\n");
                    writer.Write("WebSocket-Origin: " + m_webSocketOrigin + "\r\n");
                    writer.Write("WebSocket-Location: " + m_webSocketLocation + "\r\n");
                    writer.Write("\r\n");
                    writer.Flush();

                    return memory.ToArray();
                }
            }
        }

        private string HandleWebSocketReceive(WSAgent agent, Socket socket, IPEndPoint remoteEndPoint, SocketAsyncEventArgs args)
        {
            if (agent == null)
            {
                //m_log.Debug("Sending handshake response to " + s.RemoteEndPoint);
                //string request = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);

                agent = new WSAgent(m_server, m_throttle, m_throttleRates, UUID.Random(), UUID.Random(), socket, false);
                m_clients.Add(agent.ID, remoteEndPoint, agent);

                // This is an initial handshake
                byte[] response = BuildHandshake();
                SendMessageFinal(socket, response);

                WebSocketClientConnectedEventHandler handler = Connected;
                if (handler != null)
                    handler(agent);

                return null;
            }

            int start = args.Offset;
            int end = start + args.BytesTransferred;

            // If we are not already reading something, look for the start byte 0x00
            if (!agent.ReadingData)
            {
                for (start = 0; start < end; start++)
                {
                    if (args.Buffer[start] == 0x00)
                    {
                        agent.ReadingData = true; // We found the start byte and can now start reading
                        agent.DataString = new StringBuilder();
                        start++; // Don't include the start byte in the string
                        break;
                    }
                }
            }

            if (agent.ReadingData)
            {
                bool endIsInThisBuffer = false;

                // Look for the end byte 0xFF
                for (int i = start; i < end; i++)
                {
                    if (args.Buffer[i] == 0xFF)
                    {
                        // We found the ending byte
                        endIsInThisBuffer = true;
                        break;
                    }
                }

                // Append this data into the string builder
                agent.DataString.Append(Encoding.UTF8.GetString(args.Buffer, start, end - start));

                // The end is in this buffer, which means we can construct a message
                if (endIsInThisBuffer)
                {
                    // We are no longer reading data
                    agent.ReadingData = false;
                    string data = agent.DataString.ToString();
                    agent.DataString = null;

                    return data;
                }
            }

            return null;
        }

        #endregion Web Socket Handling

        /// <summary>
        /// Creates an instance of the SocketAsyncEventArgs class
        /// </summary>
        private SocketAsyncEventArgs CreateSocketArgs()
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.Completed += OnIOCompleted;
            args.SetBuffer(new byte[m_receiveBufferSize], 0, m_receiveBufferSize);
            return args;
        }
    }
}
