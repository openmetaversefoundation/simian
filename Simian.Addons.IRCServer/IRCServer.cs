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

using OpenMetaverse;
using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using log4net;
using Nini.Config;

namespace Simian.Addons.IRCServer
{
    [ApplicationModule("IRCServer")]
    public class IRCServer : IApplicationModule
    {
        const int TCP_BUFFER_LENGTH = 4096;

        #region Nested Classes

        public class IRCUser : IScenePresence
        {
            private string m_name;
            private IScene m_scene;
            private UUID m_id;
            private Vector3 m_position;
            private Quaternion m_rotation = Quaternion.Identity;
            private Vector3 m_lastPosition;
            private Quaternion m_lastRotation = Quaternion.Identity;
            private Vector3 m_lastSignificantPosition;

            public string UserName;
            public string HostName;
            public string IRCRealName;
            public TcpClient TCPClient;
            public byte[] Buffer = new byte[TCP_BUFFER_LENGTH];
            public IScenePresence Presence;

            public IRCUser(IScene scene, string name, UUID id)
            {
                m_scene = scene;
                m_name = name;
                m_id = id;
                m_position = new Vector3(((scene.MaxPosition + scene.MinPosition) * 0.5d) - scene.MinPosition);
            }

            #region Properties

            public string Name { get { return m_name; } set { m_name = value; } }
            public IScene Scene { get { return m_scene; } }
            public UUID ID { get { return m_id; } }
            public uint LocalID { get { return 0; } } // Hide IRC users
            public UUID OwnerID { get { return m_id; } set { } }
            public UUID CreatorID { get { return m_id; } set { } }
            public UUID GroupID { get { return UUID.Zero; } set { } }
            public Vector3 Scale
            {
                get
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        return presence.Scale;
                    else
                        return Vector3.One;
                }
                set
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        presence.Scale = value;
                }
            }
            public Vector3 ScenePosition
            {
                get
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        return presence.ScenePosition;
                    else
                        return m_position;
                }
            }
            public Vector3 RelativePosition
            {
                get
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        return presence.RelativePosition;
                    else
                        return m_position;
                }
                set
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        presence.RelativePosition = value;
                    else
                        m_position = value;
                }
            }
            public Quaternion SceneRotation
            {
                get
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        return presence.SceneRotation;
                    else
                        return m_rotation;
                }
            }
            public Quaternion RelativeRotation
            {
                get
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        return presence.RelativeRotation;
                    else
                        return m_rotation;
                }
                set
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        presence.RelativeRotation = value;
                    else
                        m_rotation = value;
                }
            }
            public AABB SceneAABB
            {
                get
                {
                    Vector3 center = ScenePosition;
                    Vector3 halfExtent = Scale * 0.5f;
                    return new AABB(center - halfExtent, center + halfExtent);
                }
            }
            public bool IsVerified
            {
                get { return false; }
                set { }
            }
            public bool IsChildPresence
            {
                get { return false; }
                set { }
            }
            public IInterestList InterestList { get { return null; } }
            public float InterestRadius { get { return 0.0f; } }
            public Vector3 LastRelativePosition
            {
                get
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        return presence.LastRelativePosition;
                    else
                        return m_lastPosition;
                }
                set
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        presence.LastRelativePosition = value;
                    else
                        m_lastPosition = value;
                }
            }
            public Quaternion LastRelativeRotation
            {
                get
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        return presence.LastRelativeRotation;
                    else
                        return m_lastRotation;
                }
                set
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        presence.LastRelativeRotation = value;
                    else
                        m_lastRotation = value;
                }
            }
            public Vector3 LastSignificantPosition
            {
                get
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        return presence.LastSignificantPosition;
                    else
                        return m_lastSignificantPosition;
                }
                set
                {
                    IScenePresence presence = Presence;
                    if (presence != null)
                        presence.LastSignificantPosition = value;
                    else
                        m_lastSignificantPosition = value;
                }
            }

            #endregion Properties

            public override string ToString()
            {
                return String.Format("IRCUser \"{0}\" ({1}) @ {2}", Name, ID, m_position);
            }
        }

        public class IRCChannel
        {
            public string Name;
            public string Topic;
            public string CreatorName;
            public DateTime CreatedTime;
            public string Modes;
        }

        #endregion Nested Classes

        #region Enums

        public enum IRCMessageType
        {
            Join,
            Part,
            Nick,
            Privmsg,
            Quit
        }

        #endregion Enums

        #region Delegates

        public delegate void ClientConnectedHandler(IRCUser user);
        public delegate void ClientDisconnectedHandler(IRCUser user, string reason);
        public delegate void DataReceivedHandler(IRCUser user, string message);

        #endregion Delegates

        #region Events

        public event ClientConnectedHandler ClientConnected;
        public event ClientDisconnectedHandler ClientDisconnected;
        public event DataReceivedHandler DataReceived;

        #endregion Events

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private Simian m_simian;
        private ISceneFactory m_sceneFactory;
        private TcpListener m_listener;
        private AsyncCallback m_clientConnectedCallback;
        private AsyncCallback m_dataReceivedCallback;
        private List<IRCUser> m_users = new List<IRCUser>();
        private List<KeyValuePair<IRCChannel, IRCUser>> m_channelUserPairs = new List<KeyValuePair<IRCChannel, IRCUser>>();
        private string[] m_motd;
        private IScene m_scene;
        private string m_defaultChannel;

        public bool Start(Simian simian)
        {
            m_simian = simian;

            if (File.Exists("motd.txt"))
                m_motd = File.ReadAllLines("motd.txt");

            else m_motd = new string[0];

            m_sceneFactory = m_simian.GetAppModule<ISceneFactory>();
            if (m_sceneFactory != null)
            {
                m_sceneFactory.OnSceneStart += SceneStartHandler;
            }

            m_clientConnectedCallback = new AsyncCallback(
                delegate(IAsyncResult ar)
                {
                    if (m_scene == null)
                        return;

                    try
                    {
                        //accept connection for a new user
                        IRCUser user = new IRCUser(m_scene, null, UUID.Random());
                        user.TCPClient = m_listener.EndAcceptTcpClient(ar);
                        m_log.Info("Connection from " + user.TCPClient.Client.RemoteEndPoint);

                        //add new user to dictionary
                        m_users.Add(user);

                        //begin listening for data on the new connection, and also for the next client connection
                        user.TCPClient.Client.BeginReceive(user.Buffer, 0, TCP_BUFFER_LENGTH, SocketFlags.None, m_dataReceivedCallback, user);
                        m_listener.BeginAcceptTcpClient(m_clientConnectedCallback, user);

                        //fire ClientConnected event
                        if (ClientConnected != null)
                            ClientConnected(user);
                    }
                    catch (ObjectDisposedException) { }
                }
            );

            m_dataReceivedCallback = new AsyncCallback(
                delegate(IAsyncResult ar)
                {
                    IRCUser user = (IRCUser)ar.AsyncState;

                    //number of bytes read
                    int bytesRead = user.TCPClient.Client.EndReceive(ar);

                    //name or address, depending on whether or not user is authenticated
                    string displayName = user.Name == null ? user.TCPClient.Client.RemoteEndPoint.ToString() : user.Name;

                    //check for disconnection
                    if (bytesRead == 0 || !user.TCPClient.Client.Connected)
                    {
                        m_log.Info("Client " + displayName + " disconnected.");

                        SendToUserChannels(user, IRCMessageType.Quit, "Disconnected"); //TODO: quit messages

                        //remove user from disctionary
                        RemoveUser(user);

                        //fire ClientDisconnected event
                        if (ClientDisconnected != null)
                            ClientDisconnected(user, null);

                        return;
                    }

                    //trim buffer length to the number of bytes read
                    byte[] buffer = new byte[bytesRead];
                    Array.Copy(user.Buffer, 0, buffer, 0, bytesRead);

                    //split message into string lines
                    string[] delimiters = new string[] { "\0", "\r", "\n" };
                    string[] lines = Encoding.ASCII.GetString(buffer).Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

                    //parse each line
                    for (int i = 0, len = lines.Length; i < len; i++)
                    {
                        m_log.Info("<" + displayName + ">: " + lines[i]);

                        //fire DataReceived event
                        if (DataReceived != null)
                            DataReceived(user, lines[i]);

                        ParseCommand(user, lines[i]);
                    }

                    //keep listening for data
                    try
                    {
                        user.TCPClient.Client.BeginReceive(user.Buffer, 0, TCP_BUFFER_LENGTH, SocketFlags.None, m_dataReceivedCallback, user);
                    }
                    catch (Exception ex)
                    {
                        m_log.Warn(ex.Message);
                        RemoveUser(user);
                    }
                }
            );

            try
            {
                m_listener = new TcpListener(IPAddress.Any, 6667);
                m_listener.Start();
                m_listener.BeginAcceptTcpClient(m_clientConnectedCallback, this);
            }
            catch (Exception ex)
            {
                m_log.Error("Failed to start IRC server: " + ex.Message);
                return false;
            }

            m_log.Info("IRC server listening on port 6667");
            return true;
        }

        public void Stop()
        {
            if (m_listener != null)
                m_listener.Stop();

            if (m_sceneFactory != null)
            {
                m_sceneFactory.OnSceneStart -= SceneStartHandler;
            }

            //TODO: clean up m_users
        }


        /// <summary>
        /// Parses an individual command from the client
        /// </summary>
        /// <param name="user"></param>
        /// <param name="commandString"></param>
        private void ParseCommand(IRCUser user, string commandString)
        {
            string[] words = commandString.Split(' ');
            string command = words[0].ToLower();

            if (words.Length < 1) //TODO: proper check of param count for all commands
            {
                SendToUser(user, ":Simian 461 " + user.Name + " " + words[0].ToUpper() + " Not enough parameters");
                return;
            }

            switch (command)
            {
                case "ison":
                    List<string> online = new List<string>();
                    for (int i = 1; i < words.Length; i++)
                    {
                        IRCUser checkUser = GetUserByName(words[i]);
                        if (checkUser != null)
                            online.Add(checkUser.Name);
                    }

                    SendToUser(user, ":Simian 303 " + user.Name + " :" + String.Join(" ", online.ToArray()));
                    break;

                case "join":
                    JoinUserToChannel(user, words[1]);
                    break;

                case "mode":
                    //TODO?
                    break;

                case "names":
                    IRCChannel channel = GetChannelByName(words[1]);
                    if (channel != null)
                        SendNamesList(user, channel);
                    break;

                case "nick":
                    SetUserNick(user, words[1]);
                    break;

                case "part":
                    PartUserFromChannel(user, words[1]);
                    break;

                case "ping":
                    SendToUser(user, "PONG " + words[1]);
                    break;

                case "user":
                    bool newUser = user.Name == null || user.HostName == null;

                    if (newUser)
                    {
                        if (words.Length < 5) return;
                        string[] ircRealName = new string[words.Length - 4];
                        Array.Copy(words, 4, ircRealName, 0, ircRealName.Length);
                        user.IRCRealName = String.Join(" ", ircRealName);
                        user.HostName = "irc";
                        user.UserName = words[1];
                    }

                    if (newUser && user.Name != null && user.HostName != null)
                        SendWelcome(user);
                    break;

                case "userhost":
                    IRCUser targetUser = GetUserByName(words[1]);
                    if (targetUser != null)
                        SendToUser(user, ":Simian 302 " + user.Name + " :" + targetUser.Name + "=+" + targetUser.UserName + "@" + targetUser.HostName);
                    else
                        SendToUser(user, ":Simian 302 " + user.Name + " :");
                    break;

                case "who":
                    SendWhoList(user, words[1]);
                    break;

                case "privmsg":
                    string[] messageWords = new string[words.Length - 2];
                    Array.Copy(words, 2, messageWords, 0, messageWords.Length);
                    SendMessage(user, words[1], String.Join(" ", messageWords).Substring(1));
                    break;

                default:
                    SendToUser(user, ":Simian 421 " + user.Name + " " + command.ToUpper() + " Unknown command");
                    break;
            }
        }

        /// <summary>
        /// Returns the channel object for a non case-sensitive name, or null if the channel does not exist
        /// </summary>
        /// <param name="channelName"></param>
        /// <returns></returns>
        private IRCChannel GetChannelByName(string channelName)
        {
            IRCChannel channel;

            lock (m_channelUserPairs)
            {
                string lowerName = channelName.ToLower();

                channel = m_channelUserPairs.Find(delegate(KeyValuePair<IRCChannel, IRCUser> kvp)
                {
                    return lowerName == kvp.Key.Name.ToLower();
                }).Key;
            }

            return channel;
        }

        /// <summary>
        /// Returns the user object for a non case-sensitive name, or null if the user does not exist
        /// </summary>
        /// <param name="channelName"></param>
        /// <returns></returns>
        private IRCUser GetUserByName(string userName)
        {
            IRCUser user;

            lock (m_channelUserPairs)
            {
                string lowerName = userName.ToLower();

                user = m_channelUserPairs.Find(delegate(KeyValuePair<IRCChannel, IRCUser> kvp)
                {
                    return lowerName == kvp.Value.Name.ToLower();
                }).Value;
            }

            return user;
        }

        /// <summary>
        /// Makes a user join the specified channel
        /// </summary>
        /// <param name="user"></param>
        /// <param name="channelName"></param>
        private void JoinUserToChannel(IRCUser user, string channelName)
        {
            //find all channels that this user is in
            List<KeyValuePair<IRCChannel, IRCUser>> channels = m_channelUserPairs.FindAll(delegate(KeyValuePair<IRCChannel, IRCUser> kvp)
            {
                return kvp.Value == user;
            });

            IRCChannel channel = GetChannelByName(channelName);

            if (channel == null)
            {
                //channel does not exist, so create a new one
                channel = new IRCChannel();
                channel.Name = channelName;
                channel.CreatedTime = DateTime.Now;
                channel.CreatorName = user.Name;
                channel.Topic = "";
            }
            else
            {
                for (int i = 0, cCount = channels.Count; i < cCount; i++)
                {
                    if (channels[i].Key.Name.ToLower() == channel.Name.ToLower())
                        return; //user is already in this channel
                }
            }

            m_channelUserPairs.Add(new KeyValuePair<IRCChannel, IRCUser>(channel, user));

            SendToChannel(channel, user, IRCMessageType.Join, null);

            SendToUser(user, ":Simian 332 " + user.Name + " " + channel.Name + " :" + channel.Topic);
            SendToUser(user, ":Simian 333 " + user.Name + " " + channel.Name + " " + channel.CreatorName + " " + Utils.DateTimeToUnixTime(channel.CreatedTime));

            SendNamesList(user, channel);
        }

        /// <summary>
        /// Makes a user part the specified channel
        /// </summary>
        /// <param name="user"></param>
        /// <param name="channelName"></param>
        private void PartUserFromChannel(IRCUser user, string channelName)
        {
            IRCChannel channel = GetChannelByName(channelName);

            KeyValuePair<IRCChannel, IRCUser> kvp = new KeyValuePair<IRCChannel, IRCUser>(channel, user);

            if (channel == null || !m_channelUserPairs.Contains(kvp))
                return;

            SendToChannel(channel, user, IRCMessageType.Part, null);

            if (channel.Name == m_defaultChannel)
                SendToChannel(channel, user, IRCMessageType.Join, null);

            m_channelUserPairs.Remove(kvp);
        }

        /// <summary>
        /// Returns true if the supplied nickname is already in use
        /// </summary>
        /// <param name="nickname"></param>
        /// <returns></returns>
        private bool NicknameInUse(string nickname)
        {
            foreach (IRCUser user in m_users)
                if (user.Name == nickname) return true;

            return false;
        }

        /// <summary>
        /// Sends s message to everyone in a certain channel
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="user"></param>
        /// <param name="type"></param>
        private void SendToChannel(IRCChannel channel, IRCUser user, IRCMessageType type, string extraParam)
        {
            //find all users who are in this channel
            List<KeyValuePair<IRCChannel, IRCUser>> users = m_channelUserPairs.FindAll(delegate(KeyValuePair<IRCChannel, IRCUser> kvp)
            {
                return kvp.Key == channel;
            });

            string message;

            if (type == IRCMessageType.Join)
                message = ":" + user.Name + "!" + user.UserName + "@" + user.HostName + " JOIN " + channel.Name;
            else if (type == IRCMessageType.Part)
                message = ":" + user.Name + "!" + user.UserName + "@" + user.HostName + " PART :" + channel.Name;
            else if (type == IRCMessageType.Privmsg)
            {
                message = ":" + user.Name + "!" + user.UserName + "@" + user.HostName + " PRIVMSG " + channel.Name + " :" + extraParam;

                if (channel.Name == m_defaultChannel && user.TCPClient != null) //only call entity chat for irc users
                    m_scene.EntityChat(this, user, 0f, extraParam, 0, EntityChatType.Broadcast);
            }
            else return;

            for (int u = 0, uCount = users.Count; u < uCount; u++)
            {
                if (type != IRCMessageType.Privmsg || users[u].Value != user)
                    SendToUser(users[u].Value, message);
            }
        }

        /// <summary>
        /// Sends a message to a specific user
        /// </summary>
        /// <param name="user"></param>
        /// <param name="message"></param>
        private void SendToUser(IRCUser user, string message)
        {
            if (user.TCPClient == null)
                return; //not a real irc user

            try
            {
                user.TCPClient.Client.Send(Encoding.ASCII.GetBytes(message + "\r\n"));
            }
            catch (Exception ex)
            {
                m_log.Warn(ex.Message);
            }
        }

        /// <summary>
        /// Sends a message to everyone who is on a common channel with a certain user
        /// </summary>
        /// <param name="user"></param>
        /// <param name="type"></param>
        /// <param name="extraParam"></param>
        private void SendToUserChannels(IRCUser user, IRCMessageType type, string extraParam)
        {
            //find all channels that this user is in
            List<KeyValuePair<IRCChannel, IRCUser>> channels = m_channelUserPairs.FindAll(delegate(KeyValuePair<IRCChannel, IRCUser> kvp)
            {
                return kvp.Value == user;
            });

            string message;

            if (type == IRCMessageType.Nick) //extraParam = old nickname
                message = ":" + extraParam + "!" + user.UserName + "@" + user.HostName + " NICK :" + user.Name;

            else if (type == IRCMessageType.Quit) //extraParam = quit message
                message = ":" + user.Name + "!" + user.UserName + "@" + user.HostName + " QUIT :" + extraParam;

            else return;

            SendToUser(user, message);

            List<IRCUser> recepients = new List<IRCUser>();

            //find all users who are in each channel which have not already received this message
            for (int i = 0, cCount = channels.Count; i < cCount; i++)
            {
                List<KeyValuePair<IRCChannel, IRCUser>> users = m_channelUserPairs.FindAll(delegate(KeyValuePair<IRCChannel, IRCUser> kvp)
                {
                    return kvp.Key == channels[i].Key;
                });

                for (int u = 0, uCount = users.Count; u < uCount; u++)
                {
                    if (!recepients.Contains(users[i].Value))
                    {
                        recepients.Add(users[i].Value);
                        SendToUser(users[i].Value, message);
                    }
                }
            }
        }

        /// <summary>
        /// Sends the message to populate an IRC client's names list
        /// </summary>
        /// <param name="user"></param>
        /// <param name="channel"></param>
        private void SendNamesList(IRCUser user, IRCChannel channel)
        {
            //find all users who are in this channel
            List<KeyValuePair<IRCChannel, IRCUser>> users = m_channelUserPairs.FindAll(delegate(KeyValuePair<IRCChannel, IRCUser> kvp)
            {
                return kvp.Key == channel;
            });

            List<string> names = new List<string>();

            foreach (KeyValuePair<IRCChannel, IRCUser> kvp in users)
            {
                string prefix = "";
                names.Add(prefix + kvp.Value.Name);
            }

            SendToUser(user, ":Simian 353 " + user.Name + " @ " + channel.Name + " :" + String.Join(" ", names.ToArray()));
            SendToUser(user, ":Simian 366 " + user.Name + " " + channel.Name + " :End of /NAMES list.");
        }

        /// <summary>
        /// Sends a list of user and address information
        /// </summary>
        /// <param name="user"></param>
        /// <param name="channel"></param>
        private void SendWhoList(IRCUser user, string filter)
        {
            if (filter.StartsWith("#") || filter.StartsWith("&"))
            {
                IRCChannel channel = GetChannelByName(filter);
                if (channel == null)
                {
                    //TODO: channel does not exist error?
                    return;
                }

                //find all users who are in this channel
                List<KeyValuePair<IRCChannel, IRCUser>> users = m_channelUserPairs.FindAll(delegate(KeyValuePair<IRCChannel, IRCUser> kvp)
                {
                    return kvp.Key == channel;
                });

                List<string> names = new List<string>();

                foreach (KeyValuePair<IRCChannel, IRCUser> kvp in users)
                {
                    SendToUser(user, ":Simian 352 " + user.Name + " " + channel.Name + " " + kvp.Value.UserName + " " + kvp.Value.HostName + " Simian " + kvp.Value.Name + " H 0 " + kvp.Value.IRCRealName);
                }
                SendToUser(user, ":Simian 315 " + user.Name + " " + channel.Name + " End of /WHO list.");
            }
            else
            {
                //TODO: support name/host filters?
            }
        }

        /// <summary>
        /// Send initial welcome message to a user
        /// </summary>
        /// <param name="user"></param>
        private void SendWelcome(IRCUser user)
        {
            SendToUser(user, ":Simian 001 " + user.Name + " :Welcome to " + m_scene.Name + "!");
            SendToUser(user, ":Simian 005 " + user.Name + " IRCD=Simian CHANTYPES=#&");

            for (int i = 0, len = m_motd.Length; i < len; i++)
                SendToUser(user, ":Simian 372 " + user.Name + " :" + m_motd[i]);

            SendToUser(user, ":Simian 376 " + user.Name + " :End of /MOTD command.");

            JoinUserToChannel(user, m_defaultChannel);
        }

        /// <summary>
        /// Sends an IRC Privmsg to a user or a channel
        /// </summary>
        /// <param name="user"></param>
        /// <param name="target"></param>
        /// <param name="message"></param>
        private void SendMessage(IRCUser user, string target, string message)
        {
            string lowerTarget = target.ToLower();

            if (lowerTarget.StartsWith("#") || lowerTarget.StartsWith("&"))
            {
                IRCChannel targetChannel = GetChannelByName(target);
                if (targetChannel != null)
                {
                    SendToChannel(targetChannel, user, IRCMessageType.Privmsg, message);
                    return;
                }
                else
                    SendToUser(user, ":Simian 403 " + user.Name + " " + target + " :No such nickname");
            }
            else
            {
                IRCUser targetUser = GetUserByName(target);
                if (targetUser != null)
                {
                    SendToUser(targetUser, ":" + user.Name + "!" + user.UserName + "@" + user.HostName + " PRIVMSG " + targetUser.Name + " :" + message);
                    return;
                }
                else
                    SendToUser(user, ":Simian 403 " + user.Name + " " + target + " :No such channel");
            }
        }

        /// <summary>
        /// Sets a user's nickname, or sends \"already in use\" message if the name is taken
        /// </summary>
        /// <param name="user"></param>
        /// <param name="nickname"></param>
        private void SetUserNick(IRCUser user, string nickname)
        {
            //TODO: regex character filter to alphanumeric only
            string trimmedName = nickname.Trim(':', ' ', ',', '.', '?', '\'', '"', '*', '!', '@', '+', '%', '#', '$', '(', ')');

            if (NicknameInUse(trimmedName))
                SendToUser(user, ":Simian 433 * " + nickname + " Nickname is already in use.");
            else
            {
                string oldName = user.Name == null ? "*" : user.Name;

                bool newName = user.Name == null || user.HostName == null;

                user.Name = trimmedName;

                if (newName && user.HostName != null)
                    SendWelcome(user);

                else if (user.HostName != null)
                    SendToUserChannels(user, IRCMessageType.Nick, oldName);
            }
        }

        /// <summary>
        /// Removes a user from the list, and all related entries in m_channelUserPairs
        /// </summary>
        /// <param name="user"></param>
        private void RemoveUser(IRCUser user)
        {
            if (m_users.Contains(user))
                m_users.Remove(user);

            List<KeyValuePair<IRCChannel, IRCUser>> remove = new List<KeyValuePair<IRCChannel, IRCUser>>();
            int i, len;
            for (i = 0, len = m_channelUserPairs.Count; i < len; i++)
            {
                if (m_channelUserPairs[i].Value == user)
                    remove.Add(m_channelUserPairs[i]);
            }
            for (i = 0, len = remove.Count; i < len; i++)
            {
                SendToChannel(remove[i].Key, remove[i].Value, IRCMessageType.Quit, null);
                m_channelUserPairs.Remove(remove[i]);
            }
        }


        void SceneStartHandler(IScene scene)
        {
            if (m_scene == null)
            {
                IScene[] scenes = m_sceneFactory.GetScenes();
                if (scenes != null && scenes.Length > 0)
                {
                    m_scene = scenes[0];
                    m_defaultChannel = "#" + m_scene.Name.Replace(' ', '_');

                    m_scene.OnPresenceAdd += new EventHandler<PresenceArgs>(m_scene_OnPresenceAdd);
                    m_scene.OnPresenceRemove += new EventHandler<PresenceArgs>(m_scene_OnPresenceRemove);
                    m_scene.OnEntityChat += new EventHandler<ChatArgs>(m_scene_OnEntityChat);
                }
            }
        }

        void m_scene_OnEntityChat(object sender, ChatArgs e)
        {
            IRCChannel channel = GetChannelByName(m_defaultChannel);

            if (channel != null)
            {
                IRCUser user = GetUserByName(e.Source.Name.Replace(' ', '_'));

                if (user != null && user.TCPClient == null)
                    SendToChannel(channel, user, IRCMessageType.Privmsg, e.Message);
            }
        }

        void m_scene_OnPresenceAdd(object sender, PresenceArgs e)
        {
            string name = e.Presence.Name.Replace(' ', '_');
            IRCUser user = new IRCUser(m_scene, name, e.Presence.ID);
            user.Name = name;
            user.UserName = "viewer";
            user.IRCRealName = "";
            user.HostName = "simian";
            user.Presence = e.Presence;

            lock (m_users)
            {
                if (!m_users.Contains(user))
                    m_users.Add(user);
            }

            JoinUserToChannel(user, m_defaultChannel);
        }

        void m_scene_OnPresenceRemove(object sender, PresenceArgs e)
        {
            IRCUser user = m_users.Find(delegate(IRCUser u) { return u.Name == e.Presence.Name.Replace(' ', '_'); });

            IRCChannel channel = GetChannelByName(m_defaultChannel);

            if (channel != null)
                SendToChannel(channel, user, IRCMessageType.Part, null);

            RemoveUser(user);
        }

    }
}
