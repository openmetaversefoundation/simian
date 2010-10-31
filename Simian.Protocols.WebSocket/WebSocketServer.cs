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
using System.Security.Cryptography;
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

        public void SendMessage(WSAgent agent, OSD message, ThrottleCategory category)
        {
            byte[] messageData = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(message));
            byte[] data = BuildMessageBuffer_00(messageData, true);

            SendMessageData(agent, data, category);
        }

        public void BroadcastMessage(OSD message, ThrottleCategory category)
        {
            byte[] messageData = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(message));
            byte[] data = BuildMessageBuffer_00(messageData, true);

            m_clients.ForEach(delegate(WSAgent agent) { SendMessageData(agent, data, category); });
        }

        /// <summary>
        /// Given a buffer of data, create the variable length header and
        /// prepend that to the data and return buffer to be sent.
        /// This routine does it for version 00 (May 23, 2010) of the WebSocket specification.
        /// </summary>
        /// <param name="messageData"></param>
        /// <param name="isText">true if the data is UTF8 encoded text</param>
        /// <returns></returns>
        private byte[] BuildMessageBuffer_00(byte[] messageData, bool isText) {
            byte[] data;

            data = new byte[messageData.Length + 2];
            data[0] = 0;
            Buffer.BlockCopy(messageData, 0, data, 1, messageData.Length);
            data[messageData.Length + 1] = 0xff;
            return data;
        }
        /// <summary>
        /// Given a buffer of data, create the variable length header and
        /// prepend that to the data and return buffer to be sent.
        /// This routine does it for version 03 of teh WebSocket specification.
        /// </summary>
        /// <param name="messageData"></param>
        /// <param name="isText">true if the data is UTF8 encoded text</param>
        /// <returns></returns>
        private byte[] BuildMessageBuffer_03(byte[] messageData, bool isText) {
            byte[] data;

            int typeCode = isText ? 4 : 5;
            int dataStart = 2;
            // depending on the length of the message, the header is varaible sized
            if (messageData.Length < 0x7e) {
                data = new byte[messageData.Length + 2];
                data[0] = (byte)typeCode;    // text data
                data[1] = (byte)messageData.Length;
                dataStart = 2;
            }
            else {
                if (messageData.Length > 0xffff) {
                    data = new byte[messageData.Length + 6];
                    data[0] = (byte)typeCode;    // text data
                    data[1] = (byte)0x7f;
                    data[2] = (byte)((messageData.Length >> 24) & 0xff);
                    data[3] = (byte)((messageData.Length >> 16) & 0xff);
                    data[4] = (byte)((messageData.Length >> 8) & 0xff);
                    data[5] = (byte)((messageData.Length) & 0xff);
                    dataStart = 6;
                }
                else {
                    data = new byte[messageData.Length + 4];
                    data[0] = (byte)typeCode;    // text data
                    data[1] = (byte)0x7e;
                    data[2] = (byte)((messageData.Length >> 8) & 0xff);
                    data[3] = (byte)((messageData.Length) & 0xff);
                    dataStart = 4;
                }
            }
            // put the data after the variable length header
            Buffer.BlockCopy(messageData, 0, data, dataStart, messageData.Length);
            return data;
        }

        private void SendMessageData(WSAgent agent, byte[] data, ThrottleCategory category)
        {
            // TODO: Throttling
            SendMessageFinal(agent.Socket, data);
        }

        private void SendMessageFinal(Socket destination, byte[] data)
        {
            // m_log.DebugFormat("SendMessageFinal: msg=//{0}//", Encoding.UTF8.GetString(data));
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.Completed += OnIOCompleted;
            args.SetBuffer(data, 0, data.Length);
            args.RemoteEndPoint = destination.RemoteEndPoint;

            try
            {
                if (!destination.SendAsync(args))
                    ProcessSendComplete(args);
            }
            catch (Exception ex)
            {
                m_log.Warn("Failed to send data to " + destination.RemoteEndPoint + ": " + ex.Message);
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
            // m_log.Debug("OnIOCompleted(): LastOperation=" + e.LastOperation);

            // Determine which type of operation just completed and call the associated handler
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceiveComplete(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSendComplete(e);
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
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
            m_log.Debug("ProcessAccept() from " + e.AcceptSocket.RemoteEndPoint);

            // Get the socket for the accepted client connection and put it into the 
            // ReadEventArg object user token
            SocketAsyncEventArgs readEventArgs = m_readPool.Pop();
            readEventArgs.UserToken = e.AcceptSocket;

            // As soon as the client is connected, post a receive to the connection
            if (!e.AcceptSocket.ReceiveAsync(readEventArgs))
                ProcessReceiveComplete(readEventArgs);

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
        private void ProcessReceiveComplete(SocketAsyncEventArgs e)
        {
            // m_log.Debug("ProcessReceiveComplete(): BytesTransferred=" + e.BytesTransferred);

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
                    string message = HandleWebSocketReceive_00(agent, s, remoteEndPoint, e);
                    if (message != null)
                    {
                        // Trigger the data received event
                        WebSocketDataReceivedEventHandler handler = DataReceived;
                        if (handler != null)
                            handler(agent, message);
                    }

                    // Read the next block of data send from the client
                    if (!s.ReceiveAsync(e))
                        ProcessReceiveComplete(e);

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
        private void ProcessSendComplete(SocketAsyncEventArgs e)
        {
            // m_log.Debug("ProcessSendComplete(): SocketError=" + e.SocketError + ", BytesTransferred=" + e.BytesTransferred);

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

        /// <summary>
        /// Process a reception conforming to rev 00 of the WebSocket spec
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="socket"></param>
        /// <param name="remoteEndPoint"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private string HandleWebSocketReceive_00(WSAgent agent, Socket socket, IPEndPoint remoteEndPoint, SocketAsyncEventArgs args)
        {
            if (agent == null)
            {
                // m_log.Debug("Sending handshake response to " + remoteEndPoint);
                byte[] byteRequest = new byte[args.BytesTransferred];
                Buffer.BlockCopy(args.Buffer, args.Offset, byteRequest, 0, args.BytesTransferred);

                // create the controlling agent for this new stream
                agent = new WSAgent(m_server, m_throttle, m_throttleRates, UUID.Random(), UUID.Random(), socket, false);
                m_clients.Add(agent.ID, remoteEndPoint, agent);

                // there are some fields in the header that we must extract the values of
                string requestString = Encoding.UTF8.GetString(byteRequest, 0, byteRequest.GetLength(0));
                int originIndex = requestString.IndexOf("Origin: ");
                if (originIndex > 0) {
                    // if the client specified an origin, we must echo it back in the response
                    originIndex += 8;
                    int originEnd = requestString.IndexOf('\r', originIndex);
                    if (originEnd > 0) {
                        m_webSocketOrigin = requestString.Substring(originIndex, originEnd - originIndex);
                    }
                }

                // This is an initial handshake
                // Process the header and send the response
                byte[] response = BuildHandshake(byteRequest);
                SendMessageFinal(socket, response);

                // tell those who care that we are connected
                WebSocketClientConnectedEventHandler handler = Connected;
                if (handler != null)
                    handler(agent);

                return null;
            }

            // It is not the initial handshake so extract data from the header
            // and find the application data therein.
            int start = args.Offset;
            int end = start + args.BytesTransferred;

            if (args.Buffer[start] == 0 && args.Buffer[end - 1] == 0xff) {
                // we know about the spec rev0 character padding
                string dataString = Encoding.UTF8.GetString(args.Buffer, start + 1, end - start - 2);
                return dataString;
            }
            else {
                m_log.Warn("Received message not conforming to rev 00 of the WebSocket specification");
            }
            return null;
        }

        /// <summary>
        /// The WebSockets spec has been evolving. This routine processes data framing
        /// for verions 03 of the spec.
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="socket"></param>
        /// <param name="remoteEndPoint"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private string HandleWebSocketReceive_03(WSAgent agent, Socket socket, IPEndPoint remoteEndPoint, SocketAsyncEventArgs args)
        {
            if (agent == null)
            {
                // m_log.Debug("Sending handshake response to " + remoteEndPoint);
                byte[] byteRequest = new byte[args.BytesTransferred];
                Buffer.BlockCopy(args.Buffer, args.Offset, byteRequest, 0, args.BytesTransferred);

                // create the controlling agent for this new stream
                agent = new WSAgent(m_server, m_throttle, m_throttleRates, UUID.Random(), UUID.Random(), socket, false);
                m_clients.Add(agent.ID, remoteEndPoint, agent);

                // there are some fields in the header that we must extract the values of
                string requestString = Encoding.UTF8.GetString(byteRequest, 0, byteRequest.GetLength(0));
                int originIndex = requestString.IndexOf("Origin: ");
                if (originIndex > 0) {
                    // if the client specified an origin, we must echo it back in the response
                    originIndex += 8;
                    int originEnd = requestString.IndexOf('\r', originIndex);
                    if (originEnd > 0) {
                        m_webSocketOrigin = requestString.Substring(originIndex, originEnd - originIndex);
                    }
                }

                // This is an initial handshake
                // Process the header and send the response
                byte[] response = BuildHandshake(byteRequest);
                SendMessageFinal(socket, response);

                // tell those who care that we are connected
                WebSocketClientConnectedEventHandler handler = Connected;
                if (handler != null)
                    handler(agent);

                return null;
            }

            // It is not the initial handshake so extract data from the header
            // and find the application data therein.
            int start = args.Offset;
            int end = start + args.BytesTransferred;

            bool headerMore = false;
            int headerOp = 0;
            int headerLen = 0;
            try {
                headerMore = (args.Buffer[start] & 0x80) != 0;
                headerOp = args.Buffer[start] & 0x0f;
                headerLen = args.Buffer[start + 1] & 0x7f;
                if (headerLen == 0x7e) {
                    headerLen = (args.Buffer[start + 2] << 8) + args.Buffer[start + 3];
                    start += 2;
                }
                else {
                    if (headerLen == 0x7f) {
                        headerLen = args.Buffer[start + 2] << 24
                                    + args.Buffer[start + 3] << 16
                                    + args.Buffer[start + 4] << 8
                                    + args.Buffer[start + 5];
                        start += 4;
                    }
                }
                start += 2;
            }
            catch (Exception e) {
                // failure decoding the header (probably short)
                m_log.Warn("HandleWebSocketReceive: Failure parsing message header: " + e.ToString());
                return null;
            }
            if ((end - start) < headerLen) {
                // didn't receive enough data
                m_log.Warn("HandleWebSocketReceive: received less data than specified in length. Ignoring.");
                return null;
            }

            // Opcode of '1' says 'close'
            if (headerOp == 1) {
                m_log.Warn("HandleWebSocketReceive: polite request to close connection");
                // TODO:
                return null;
            }

            // Opcode of '2' says 'ping'
            if (headerOp == 2) {
                byte[] pingResponse = GeneratePingResponse();
                SendMessageFinal(socket, pingResponse);
                // The standard is undecided on whether a control message can also
                // include data. Here we presume not.
                return null;
            }
            // TODO: someday do our own pings so we'll need to process pongs

            // if specified, remember the form of the data being received
            if (headerOp == 4) agent.ReadingBinary = false;
            if (headerOp == 5) agent.ReadingBinary = true;

            if (!agent.ReadingData) {
                // not yet reading data so initialize new buffers
                agent.ReadingData = true;
                agent.DataString = new StringBuilder();
                agent.DataBinary = null;
            }

            if (agent.ReadingBinary) {
                // If binary, build up a buffer of the binary data
                int doffset = 0;
                if (agent.DataBinary == null) {
                    agent.DataBinary = new byte[headerLen];
                }
                else {
                    byte[] temp = agent.DataBinary;
                    doffset = temp.Length;
                    agent.DataBinary = new byte[doffset + headerLen];
                    Buffer.BlockCopy(temp, 0, agent.DataBinary, 0, doffset);
                }
                Buffer.BlockCopy(args.Buffer, start, agent.DataBinary, doffset, headerLen);
            }
            else {
                // if just text, get the UTF8 characters into our growing string
                agent.DataString.Append(Encoding.UTF8.GetString(args.Buffer, start, headerLen));
            }

            if (!headerMore) {
                // end of any fragmentation. no longer reading data
                agent.ReadingData = false;
                return agent.DataString.ToString();
                // Note the race condition here for the binary data.
                // Binary is not handled correctly as it can be immediately overwritten
                // by the next message.
            }
            // if this is part of a fragmented message, don't return any data this time
            return null;
        }

        private byte[] BuildHandshake(byte[] requestHeader)
        {
            byte[] response = ComputeChallangeResponseCode(requestHeader);
            using (MemoryStream memory = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memory))
                {
                    writer.Write(Encoding.UTF8.GetBytes("HTTP/1.1 101 Web Socket Protocol Handshake\r\n"));
                    writer.Write(Encoding.UTF8.GetBytes("Upgrade: WebSocket\r\n"));
                    writer.Write(Encoding.UTF8.GetBytes("Connection: Upgrade\r\n"));
                    writer.Write(Encoding.UTF8.GetBytes("Sec-WebSocket-Origin: " + m_webSocketOrigin + "\r\n"));
                    writer.Write(Encoding.UTF8.GetBytes("Sec-WebSocket-Location: " + m_webSocketLocation + "\r\n"));
                    writer.Write(Encoding.UTF8.GetBytes("\r\n"));
                    writer.Write(response);
                    writer.Flush();

                    return memory.ToArray();
                }
            }
        }

        /// <summary>
        /// Given the challange header, extract the three challange strings 
        /// and compute the response. The challange is both text and binary.
        /// The three parts are combined into one byte array that is MD5
        /// hashed before being returned as a binary byte array.
        /// </summary>
        /// <param name="requestHeader">The binary, UTF8 form of the challange</param>
        /// <returns></returns>
        private byte[] ComputeChallangeResponseCode(byte[] requestHeader) 
        {
            string requestString = Encoding.UTF8.GetString(requestHeader, 0, requestHeader.GetLength(0));
            // m_log.DebugFormat("Received: //{0}//", requestString);
            int key1index = requestString.IndexOf("Sec-WebSocket-Key1: ");
            int key2index = requestString.IndexOf("Sec-WebSocket-Key2: ");
            UInt32 key1 = DecodeCRKey(requestString, key1index + 20);
            UInt32 key2 = DecodeCRKey(requestString, key2index + 20);
            // pack the challange
            byte[] challange = new byte[16];
            byte[] key1b = System.BitConverter.GetBytes(key1);
            byte[] key2b = System.BitConverter.GetBytes(key2);
            if (System.BitConverter.IsLittleEndian) {
                Array.Reverse(key1b);
                Array.Reverse(key2b);
            }
            Buffer.BlockCopy(key1b, 0, challange, 0, 4);
            Buffer.BlockCopy(key2b, 0, challange, 4, 4);
            Buffer.BlockCopy(requestHeader, requestHeader.Length - 8, challange, 8, 8);
            // MD5 hash the challange
            MD5 md5hasher = MD5.Create();
            byte[] hash = md5hasher.ComputeHash(challange);
            // Return the response hash code
            return hash;
        }

        /// <summary>
        /// The challange comes in three pieces, two of which are strings of
        /// random characters which include the digits of a number
        /// interspersed with spaces. We must extract the digits to make a
        /// number and then divide by the number of spaces.
        /// This routine returns the number decoded from that process.
        /// </summary>
        /// <param name="req">string including the challange string</param>
        /// <param name="begin">index in string to begin extraction</param>
        /// <returns></returns>
        private UInt32 DecodeCRKey(string req, int begin) {
            int current = begin;
            Int64 packed = 0;
            int spaces = 0;
            while (current < req.Length) {
                if (req[current] == '\n') break;
                if (req[current] == ' ') {
                    spaces++;
                }
                else {
                    int digit = "0123456789".IndexOf(req[current]);
                    if (digit >= 0) {
                        packed = packed * 10 + digit;
                    }
                }
                current++;
            }
            // m_log.DebugFormat("DecodeCRKey: key='{0}'", req.Substring(begin, current-begin-1));
            // m_log.DebugFormat("DecodeCRKey:    packed={0}, spaces={1}", packed, spaces);
            // the following should not happen and this will just cause a bad response
            if (spaces == 0) spaces = 1;
            return (UInt32)(packed / spaces);
        }

        /// <summary>
        /// We've been sent a 'ping' message. Construct and send the 'pong'.
        /// </summary>
        private byte[] GeneratePingResponse() {
            byte[] response = new byte[2];
            response[0] = 3;
            response[1] = 0;
            return response;
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
