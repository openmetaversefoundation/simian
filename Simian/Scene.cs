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
using System.ComponentModel.Composition.Primitives;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text;
using System.Threading;
using HttpServer;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian
{
    [System.Diagnostics.DebuggerDisplay("{m_name} {m_id}")]
    [Export(typeof(IScene))]
    public class Scene : IScene
    {
        #region Events

        public event EventHandler<EntityAddOrUpdateArgs> OnEntityAddOrUpdate;
        public event EventHandler<EntityArgs> OnEntityRemove;
        public event EventHandler<EntitySignificantMovementArgs> OnEntitySignificantMovement;
        public event EventHandler<ChatArgs> OnEntityChat;
        public event EventHandler<PresenceArgs> OnPresenceAdd;
        public event EventHandler<PresenceArgs> OnPresenceRemove;
        public event EventHandler<PhysicalPresenceArgs> OnSendPresenceAnimations;
        public event EventHandler<PresenceAlertArgs> OnPresenceAlert;

        #endregion Events

        #region Fields

        protected static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        protected readonly UUID m_id;
        protected readonly Simian m_simian;
        protected readonly SceneGraph m_sceneGraph = new SceneGraph();
        protected readonly Dictionary<string, InterestListEventHandler> m_interestListHandlers = new Dictionary<string, InterestListEventHandler>();
        protected readonly Dictionary<string, ApiMethod> m_apiMethods = new Dictionary<string, ApiMethod>();
        protected readonly OSDMap m_extraData = new OSDMap();

        protected bool m_running;
        protected string m_name;
        protected Vector3d m_regionPosition;
        protected Vector3d m_regionSize;
        protected IConfigSource m_configSource;
        protected IAssetClient m_assetClient;
        protected ISceneModule[] m_sceneModules;
        protected Dictionary<UUID, SceneInfo> m_neighbors = new Dictionary<UUID, SceneInfo>();
        /// <summary>Used for creating new LocalIDs</summary>
        /// <remarks>Starts at 10 because low numbers may be reserved for 
        /// special objects like terrain</remarks>
        protected int m_currentLocalID = 10;
        protected IHttpServer m_httpServer;
        protected CapabilityRouter m_capabilityRouter;
        protected Dictionary<string, CommandCallback> m_commandHandlers = new Dictionary<string, CommandCallback>();
        protected Dictionary<string, Uri> m_publicCapabilities = new Dictionary<string, Uri>();

        #endregion Fields

        #region Properties

        public bool IsRunning { get { return m_running; } }
        public Simian Simian { get { return m_simian; } }
        public UUID ID { get { return m_id; } }
        public string Name { get { return m_name; } }
        public Vector3d MinPosition { get { return m_regionPosition; } }
        public Vector3d MaxPosition { get { return m_regionPosition + m_regionSize; } }
        public IConfigSource Config { get { return m_configSource; } }
        public OSDMap ExtraData { get { return m_extraData; } }
        public CapabilityRouter Capabilities { get { return m_capabilityRouter; } }

        #endregion Properties

        #region Startup / Shutdown

        public Scene(UUID sceneID, string sceneName, Vector3d scenePosition, Vector3d sceneSize, Simian simian, IConfigSource configSource)
        {
            m_simian = simian;
            m_configSource = configSource;
            m_httpServer = simian.GetAppModule<IHttpServer>();

            m_id = sceneID;
            m_name = sceneName;
            m_regionPosition = scenePosition;
            m_regionSize = sceneSize;
        }

        public void Start()
        {
            if (m_httpServer != null)
            {
                string urlFriendlySceneName = WebUtil.UrlEncode(this.Name);
                string regionPath = "/regions/" + urlFriendlySceneName;

                // Create a capability router for this scene
                string capsPath = regionPath + "/caps/";
                m_capabilityRouter = new CapabilityRouter(m_httpServer.HttpAddress.Combine(capsPath));
                m_httpServer.AddHandler(null, null, capsPath, false, false, m_capabilityRouter.RouteCapability);

                // Create the public seed capability for this scene
                AddPublicCapability("public_region_seed_capability", m_httpServer.HttpAddress.Combine("/regions/" + urlFriendlySceneName));
                m_httpServer.AddHandler("GET", null, "/regions/" + urlFriendlySceneName, true, true, PublicSeedHandler);
            }

            #region Scene Module Whitelist Loading

            // Load the scene module whitelist
            List<string> whitelist = new List<string>();

            IConfig config = m_configSource.Configs["SceneModules"];
            if (config != null)
            {
                foreach (string key in config.GetKeys())
                {
                    if (config.GetBoolean(key))
                        whitelist.Add(key);
                }
            }

            #endregion Scene Module Whitelist Loading

            #region Scene Module Loading

            IEnumerable<Lazy<object, object>> exportEnumerable = m_simian.ModuleContainer.GetExports(typeof(ISceneModule), null, null);
            Dictionary<string, Lazy<object, object>> exports = new Dictionary<string, Lazy<object, object>>();
            List<ISceneModule> imports = new List<ISceneModule>();
            List<string> notLoaded = new List<string>();

            // Reshuffle exportEnumerable into a dictionary mapping module names to their lazy instantiations
            foreach (Lazy<object, object> lazyExport in exportEnumerable)
            {
                IDictionary<string, object> metadata = (IDictionary<string, object>)lazyExport.Metadata;
                object nameObj;
                if (metadata.TryGetValue("Name", out nameObj))
                {
                    string name = (string)nameObj;

                    if (!exports.ContainsKey(name))
                        exports.Add(name, lazyExport);
                    else
                        m_log.Warn("Found an ISceneModule with a duplicate name: " + name);
                }
            }

            // Load modules in the order they appear in the whitelist
            foreach (string whitelisted in whitelist)
            {
                Lazy<object, object> lazyExport;
                if (exports.TryGetValue(whitelisted, out lazyExport))
                {
                    // Instantiate a new copy of each scene module
                    ISceneModule module = (ISceneModule)Activator.CreateInstance(lazyExport.Value.GetType());

                    imports.Add(module);
                    exports.Remove(whitelisted);
                }
                else
                {
                    notLoaded.Add(whitelisted);
                }
            }

            // Populate m_sceneModules
            m_sceneModules = imports.ToArray();

            // Start the scene modules
            for (int i = 0; i < m_sceneModules.Length; i++)
            {
                ISceneModule module = m_sceneModules[i];
                module.Start(this);

                if (module is IScriptApi)
                    RegisterScriptingApi((IScriptApi)module);
            }

            #endregion Scene Module Loading

            #region Logging

            m_log.InfoFormat("Loaded {0} scene modules for {1}", (m_sceneModules != null ? m_sceneModules.Length : 0), m_name);

            if (exports.Count > 0)
            {
                StringBuilder skippedStr = new StringBuilder("Skipped scene modules: ");
                foreach (string exportName in exports.Keys)
                    skippedStr.Append(exportName + " ");
                m_log.Info(skippedStr.ToString());
            }

            if (notLoaded.Count > 0)
            {
                StringBuilder notLoadedStr = new StringBuilder("Did not load whitelisted scene modules: ");
                foreach (string entry in notLoaded)
                    notLoadedStr.Append(entry + " ");
                m_log.Warn(notLoadedStr.ToString());
            }

            #endregion Logging

            // Add a few command handlers
            AddCommandHandler("presences", PresenceCommandHandler);
            AddCommandHandler("shutdown", ShutdownCommandHandler);
            AddCommandHandler("restart", RestartCommandHandler);

            m_running = true;
        }

        public void Stop()
        {
            m_running = false;

            m_commandHandlers.Clear();
            m_publicCapabilities.Clear();

            if (m_httpServer != null)
            {
                m_httpServer.RemoveHandlers(m_capabilityRouter.RouteCapability);
                m_httpServer.RemoveHandlers(PublicSeedHandler);
            }

            foreach (ISceneModule module in m_sceneModules)
            {
                m_log.Debug(Name + ": Stopping scene module " + module);
                module.Stop();
            }
        }

        #endregion Startup / Shutdown

        #region Public Capabilities

        public void AddPublicCapability(string capName, Uri uri)
        {
            lock (m_publicCapabilities)
                m_publicCapabilities[capName] = uri;
        }

        public bool RemovePublicCapability(string capName)
        {
            lock (m_publicCapabilities)
                return m_publicCapabilities.Remove(capName);
        }

        public bool TryGetPublicCapability(string capName, out Uri uri)
        {
            lock (m_publicCapabilities)
                return m_publicCapabilities.TryGetValue(capName, out uri);
        }

        #endregion Public Capabilities

        #region Miscellaneous

        public T GetSceneModule<T>()
        {
            foreach (ISceneModule module in m_sceneModules)
            {
                if (module is T)
                    return (T)module;
            }

            return default(T);
        }

        public uint CreateLocalID()
        {
            uint newID;

            while (true)
            {
                newID = (uint)Interlocked.Increment(ref m_currentLocalID);
                if (!m_sceneGraph.ContainsKey(newID))
                    return newID;
            }
        }

        #endregion Miscellaneous

        #region Neighbors

        public void AddNeighbor(SceneInfo neighbor)
        {
            if (neighbor.ID == this.ID)
            {
                m_log.Warn("Skipping adding ourself as a neighbor in " + this.Name);
                return;
            }

            lock (m_neighbors)
                m_neighbors[neighbor.ID] = neighbor;
        }

        public bool RemoveNeighbor(UUID neighborID)
        {
            lock (m_neighbors)
                return m_neighbors.Remove(neighborID);
        }

        public SceneInfo[] GetNeighbors()
        {
            lock (m_neighbors)
            {
                SceneInfo[] neighbors = new SceneInfo[m_neighbors.Count];

                int i = 0;
                foreach (SceneInfo neighbor in m_neighbors.Values)
                    neighbors[i++] = neighbor;

                return neighbors;
            }
        }

        public SceneInfo[] GetNeighborsNear(Vector3d position, double radius)
        {
            List<SceneInfo> neighbors = new List<SceneInfo>();
            Vector3 fPosition = new Vector3(position);
            float fRadius = (float)radius;

            lock (m_neighbors)
            {
                foreach (KeyValuePair<UUID, SceneInfo> neighbor in m_neighbors)
                {
                    AABB neighborBox = new AABB(new Vector3(neighbor.Value.MinPosition), new Vector3(neighbor.Value.MaxPosition));
                    if (SphereAABB.CollisionTest(neighborBox, fPosition, fRadius))
                        neighbors.Add(neighbor.Value);
                }
            }

            return neighbors.ToArray();
        }

        #endregion Neighbors

        #region Interest List

        public void AddInterestListHandler(string eventType, InterestListEventHandler handler)
        {
            m_interestListHandlers[eventType] = handler;
        }

        public void CreateInterestListEvent(InterestListEvent eventData)
        {
            InterestListEventHandler handler;
            if (TryGetInterestListHandler(eventData.Type, out handler))
            {
                ForEachPresence(
                    delegate(IScenePresence presence)
                    {
                        // TODO: Once we have semi-complete implementations of multiple protocols, 
                        // we'll probably want to filter events here to avoid stuffing a bunch of 
                        // cross-protocol events into the wrong queues
                        if (presence.InterestList != null)
                            presence.InterestList.EnqueueEvent(eventData, handler);
                    }
                );
            }
        }

        public void CreateInterestListEventFor(IScenePresence presence, InterestListEvent eventData)
        {
            if (presence.InterestList != null)
            {
                InterestListEventHandler handler;
                if (TryGetInterestListHandler(eventData.Type, out handler))
                {
                    presence.InterestList.EnqueueEvent(eventData, handler);
                }
            }
        }

        public bool TryGetInterestListHandler(string eventType, out InterestListEventHandler handler)
        {
            return m_interestListHandlers.TryGetValue(eventType, out handler);
        }

        #endregion Interest List

        #region Scripting

        public bool TryGetApiMethod(string methodName, out ApiMethod apiMethod)
        {
            return m_apiMethods.TryGetValue(methodName, out apiMethod);
        }

        private void RegisterScriptingApi(IScriptApi scriptApi)
        {
            MethodInfo[] allMethods = scriptApi.GetType().GetMethods();
            int count = 0;

            for (int i = 0; i < allMethods.Length; i++)
            {
                MethodInfo method = allMethods[i];
                object[] match = method.GetCustomAttributes(typeof(ScriptMethodAttribute), false);

                if (match != null && match.Length > 0)
                {
                    m_apiMethods[method.Name] = new ApiMethod(scriptApi, method);
                    ++count;
                }
            }

            m_log.Debug("Scripting API " + scriptApi + " registered " + count + " API calls");
        }

        #endregion Scripting

        #region Command Handling

        public void AddCommandHandler(string command, CommandCallback callback)
        {
            lock (m_commandHandlers)
                m_commandHandlers[command] = callback;
        }

        public bool RemoveCommandHandler(string command)
        {
            lock (m_commandHandlers)
                return m_commandHandlers.Remove(command);
        }

        public bool HandleCommand(string command, string[] args)
        {
            if (String.IsNullOrEmpty(command))
                return false;

            CommandCallback callback;
            lock (m_commandHandlers)
                m_commandHandlers.TryGetValue(command, out callback);

            if (callback != null)
            {
                callback(command, args, false);
                return true;
            }

            return false;
        }

        public string[] GetCompletions(string complete)
        {
            List<string> completions = new List<string>();
            bool emptyString = String.IsNullOrEmpty(complete);

            // Enable special tab completion for the help command
            if (complete.StartsWith("help "))
                complete = complete.Substring(5);

            int length = complete.Length;

            lock (m_commandHandlers)
            {
                foreach (string name in m_commandHandlers.Keys)
                {
                    if (emptyString || name.StartsWith(complete))
                        completions.Add(name.Substring(length));
                }
            }

            return completions.ToArray();
        }

        public bool HelpHandler(string command, string[] args)
        {
            CommandCallback callback;
            lock (m_commandHandlers)
                m_commandHandlers.TryGetValue(command, out callback);

            if (callback != null)
            {
                callback(command, args, true);
                return true;
            }

            return false;
        }

        private void PresenceCommandHandler(string command, string[] args, bool printHelp)
        {
            if (printHelp)
            {
                Console.WriteLine("Print a list of presences in this scene");
                return;
            }

            int i = 0;
            ForEachPresence(
                delegate(IScenePresence presence)
                {
                    Console.WriteLine(presence.ToString());
                    ++i;
                }
            );
            Console.WriteLine(i + " presences in " + m_name);
        }

        private void ShutdownCommandHandler(string command, string[] args, bool printHelp)
        {
            // TODO: Implement the command line options of the Linux shutdown command,
            // including sending a message to all connected presences

            if (printHelp)
                Console.WriteLine("Bring the \"{0}\" scene down", m_name);
            else
                this.Stop();
        }

        private void RestartCommandHandler(string command, string[] args, bool printHelp)
        {
            // TODO: Implement the command line options of the Linux restart command,
            // including sending a message to all connected presences

            if (printHelp)
            {
                Console.WriteLine("Restart the \"{0}\" scene", m_name);
            }
            else
            {
                this.Stop();
                this.Start();
            }
        }

        #endregion Command Handling

        #region Entity Methods

        public void EntityChat(object sender, ISceneEntity source, float audibleDistance, string message, int channel, EntityChatType type)
        {
            EventHandler<ChatArgs> callback = OnEntityChat;
            if (callback != null)
                callback(sender, new ChatArgs { Source = source, AudibleDistance = audibleDistance, Message = message, Channel = channel, Type = type });
        }

        #endregion Entity Methods

        #region Presence Methods

        public void PresenceAlert(object sender, IScenePresence presence, string message)
        {
            EventHandler<PresenceAlertArgs> callback = OnPresenceAlert;
            if (callback != null)
                callback(sender, new PresenceAlertArgs { Presence = presence, Message = message });
        }

        public void SendPresenceAnimations(object sender, IPhysicalPresence presence)
        {
            EventHandler<PhysicalPresenceArgs> callback = OnSendPresenceAnimations;
            if (callback != null)
                callback(sender, new PhysicalPresenceArgs { Presence = presence });
        }

        #endregion Presence Methods

        #region Scene Graph Methods

        public void EntityAddOrUpdate(object sender, ISceneEntity entity, UpdateFlags updateFlags, uint extraFlags)
        {
            const float TOLERANCE = 0.01f;
            const float SIGNIFICANT_MOVEMENT_SQ = 2f * 2f;

            bool isNew = false;

            #region Entity Creation

            if (m_sceneGraph.AddOrUpdate(entity))
            {
                isNew = true;

                // If this is a scene presence, fire the callback for a new presence being added
                if (entity is IScenePresence)
                {
                    IScenePresence presence = (IScenePresence)entity;
                    m_log.Debug("Added scene presence " + presence.Name + " to scene " + this.Name);

                    EventHandler<PresenceArgs> presenceCallback = OnPresenceAdd;
                    if (presenceCallback != null)
                        presenceCallback(sender, new PresenceArgs { Presence = presence });
                }
            }

            #endregion Entity Creation

            #region Update Damping

            UpdateFlags unchangedFlags = 0;

            if (updateFlags.HasFlag(UpdateFlags.Position) && entity.RelativePosition.ApproxEquals(entity.LastRelativePosition, TOLERANCE))
                unchangedFlags |= UpdateFlags.Position;
            if (updateFlags.HasFlag(UpdateFlags.Rotation) && entity.RelativeRotation.ApproxEquals(entity.LastRelativeRotation, TOLERANCE))
                unchangedFlags |= UpdateFlags.Rotation;

            if (entity is IPhysical)
            {
                IPhysical physical = (IPhysical)entity;

                if (updateFlags.HasFlag(UpdateFlags.Acceleration) && physical.Acceleration == physical.LastAcceleration)
                    unchangedFlags |= UpdateFlags.Acceleration;
                if (updateFlags.HasFlag(UpdateFlags.AngularVelocity) && physical.AngularVelocity == physical.LastAngularVelocity)
                    unchangedFlags |= UpdateFlags.AngularVelocity;
                if (updateFlags.HasFlag(UpdateFlags.Velocity) && physical.Velocity == physical.LastVelocity)
                    unchangedFlags |= UpdateFlags.Velocity;
            }

            if (updateFlags != UpdateFlags.FullUpdate)
            {
                updateFlags &= ~unchangedFlags;
                if (updateFlags == 0 && extraFlags == 0)
                    return;
            }

            #endregion Update Damping

            // If this is a physical entity and the scale or shape changed, reset any cached mass calculations
            if (entity is IPhysical && (updateFlags.HasFlag(UpdateFlags.Scale) || updateFlags.HasFlag(UpdateFlags.Shape)))
                ((IPhysical)entity).ResetMass();

            // Mark updated entities as modified
            if (!isNew)
                entity.MarkAsModified();

            #region Callbacks

            // Fire the callback for an entity being added or updated
            EventHandler<EntityAddOrUpdateArgs> addUpdateCallback = OnEntityAddOrUpdate;
            if (addUpdateCallback != null)
                addUpdateCallback(sender, new EntityAddOrUpdateArgs { Entity = entity, UpdateFlags = updateFlags, ExtraFlags = extraFlags, IsNew = isNew });

            // Check for significant movement
            Vector3 scenePosition = entity.ScenePosition;
            if (Vector3.DistanceSquared(entity.LastSignificantPosition, scenePosition) > SIGNIFICANT_MOVEMENT_SQ)
            {
                // Fire the significant movement callback
                EventHandler<EntitySignificantMovementArgs> sigMovementCallback = OnEntitySignificantMovement;
                if (sigMovementCallback != null)
                    sigMovementCallback(sender, new EntitySignificantMovementArgs { Entity = entity });

                // Update LastSignificantPosition after the callback
                entity.LastSignificantPosition = scenePosition;
            }

            #endregion Callbacks

            #region Update ISceneEntity.Last* Properties

            if (!unchangedFlags.HasFlag(UpdateFlags.Position))
                entity.LastRelativePosition = entity.RelativePosition;
            if (!unchangedFlags.HasFlag(UpdateFlags.Rotation))
                entity.LastRelativeRotation = entity.RelativeRotation;

            if (entity is IPhysical)
            {
                IPhysical physical = (IPhysical)entity;

                if (!unchangedFlags.HasFlag(UpdateFlags.Acceleration))
                    physical.LastAcceleration = physical.Acceleration;
                if (!unchangedFlags.HasFlag(UpdateFlags.AngularVelocity))
                    physical.LastAngularVelocity = physical.AngularVelocity;
                if (!unchangedFlags.HasFlag(UpdateFlags.Velocity))
                    physical.LastVelocity = physical.Velocity;
            }

            #endregion Update ISceneEntity.Last* Properties
        }

        public bool EntityRemove(object sender, ISceneEntity entity)
        {
            EventHandler<EntityArgs> callback = OnEntityRemove;
            if (OnEntityRemove != null)
                callback(sender, new EntityArgs { Entity = entity });

            bool success = m_sceneGraph.Remove(entity);

            if (entity is IScenePresence)
            {
                EventHandler<PresenceArgs> presenceCallback = OnPresenceRemove;
                if (presenceCallback != null)
                    presenceCallback(sender, new PresenceArgs { Presence = (IScenePresence)entity });
            }

            return success;
        }

        public bool ContainsEntity(UUID id)
        {
            return m_sceneGraph.ContainsKey(id);
        }

        public bool ContainsEntity(uint localID)
        {
            return m_sceneGraph.ContainsKey(localID);
        }

        public int EntityCount()
        {
            return m_sceneGraph.EntityCount;
        }

        public bool TryGetEntity(UUID id, out ISceneEntity entity)
        {
            return m_sceneGraph.TryGetEntity(id, out entity);
        }

        public bool TryGetEntity(uint localID, out ISceneEntity entity)
        {
            return m_sceneGraph.TryGetEntity(localID, out entity);
        }

        public void ForEachEntity(Action<ISceneEntity> action)
        {
            m_sceneGraph.ForEachEntity(action);
        }

        public ISceneEntity FindEntity(Predicate<ISceneEntity> predicate)
        {
            return m_sceneGraph.FindEntity(predicate);
        }

        #endregion Scene Graph Methods

        #region Presence Methods

        public bool CanPresenceEnter(UUID agentID, ref Vector3 startPosition, ref Vector3 lookAt)
        {
            return true;
        }

        public bool CanPresenceSee(UUID agentID)
        {
            return true;
        }

        public int PresenceCount()
        {
            return m_sceneGraph.PresenceCount;
        }

        public bool TryGetPresence(UUID id, out IScenePresence presence)
        {
            return m_sceneGraph.TryGetPresence(id, out presence);
        }

        public void ForEachPresence(Action<IScenePresence> action)
        {
            m_sceneGraph.ForEachPresence(action);
        }

        public IScenePresence[] GetPresences()
        {
            return m_sceneGraph.GetPresenceArray();
        }

        public IScenePresence FindPresence(Predicate<IScenePresence> predicate)
        {
            return m_sceneGraph.FindPresence(predicate);
        }

        public int RemoveAllPresences(Predicate<IScenePresence> predicate)
        {
            // Note that this method will only remove the presences that are in
            // the scene at this point. If any new presences are added while 
            // this method is executing they will not be removed
            IScenePresence[] presences = m_sceneGraph.GetPresenceArray();

            for (int i = 0; i < presences.Length; i++)
                EntityRemove(this, presences[i]);

            return presences.Length;
        }

        #endregion Presence Methods

        private void PublicSeedHandler(IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            OSDMap responseMap = new OSDMap();

            // Return all of the public capabilities for this region
            OSDMap capabilities = new OSDMap();

            lock (m_publicCapabilities)
            {
                foreach (KeyValuePair<string, Uri> kvp in m_publicCapabilities)
                {
                    if (kvp.Key != "public_region_seed_capability")
                        capabilities[kvp.Key] = OSD.FromUri(kvp.Value);
                }
            }

            responseMap["capabilities"] = capabilities;

            WebUtil.SendJSONResponse(response, responseMap);
        }
    }
}
