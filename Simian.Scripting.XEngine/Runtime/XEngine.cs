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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;
using Microsoft.CSharp;
using Amib.Threading;
using OpenMetaverse;
using Simian.Protocols.Linden;

namespace Simian.Scripting.Linden
{
    [SceneModule("XEngine")]
    public partial class XEngine : ISceneModule, ILSLScriptEngine
    {
        #region Helper Classes

        class ScriptTimer
        {
            public UUID ScriptID;
            public long TickInterval;
            public long NextEvent;

            public ScriptTimer(UUID scriptID, long tickInterval)
            {
                ScriptID = scriptID;
                TickInterval = tickInterval;
                NextEvent = DateTime.UtcNow.Ticks + TickInterval;
            }
        }

        class KVPSorter : IComparer<KeyValuePair<int, int>>
        {
            public int Compare(KeyValuePair<int, int> a,
                    KeyValuePair<int, int> b)
            {
                return a.Key.CompareTo(b.Key);
            }
        }

        #endregion Helper Classes

        private const int XENGINE_LONG_SLEEP_INTERVAL = 100;
        private const int REQUEST_TIMEOUT = 1000 * 30;
        private static readonly UUID SCRIPT_STATE_MAGIC_ID = new UUID("db63f435-5492-4b83-bf22-97aa070f5bc2");
        private static readonly KVPSorter KVP_SORTER = new KVPSorter();

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IScheduler m_scheduler;
        private IAssetClient m_assetClient;
        private CSharpCodeProvider m_csCodeProvider = new CSharpCodeProvider();
        private Dictionary<UUID, LSLScriptInstance> m_scripts = new Dictionary<UUID, LSLScriptInstance>();
        private Dictionary<UUID, Dictionary<UUID, LSLScriptInstance>> m_scriptedEntities = new Dictionary<UUID, Dictionary<UUID, LSLScriptInstance>>();
        private Queue<LSLScriptInstance> m_scriptsWithEvents = new Queue<LSLScriptInstance>();
        private SmartThreadPool m_eventThreadPool;
        private Dictionary<UUID, ScriptTimer> m_scriptTimers = new Dictionary<UUID, ScriptTimer>();
        private bool m_running;
        private object m_syncRoot = new object();

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_scheduler = scene.Simian.GetAppModule<IScheduler>();
            if (m_scheduler == null)
            {
                m_log.Error("XEngine requires an IScheduler");
                return;
            }

            m_assetClient = scene.Simian.GetAppModule<IAssetClient>();
            if (m_assetClient == null)
            {
                m_log.Error("XEngine requires an IAssetClient");
                return;
            }

            // Build the thread pool that will run script events
            STPStartInfo eventThreadPoolInfo = new STPStartInfo
            {
                IdleTimeout = 60 * 1000,
                MinWorkerThreads = 0,
                MaxWorkerThreads = 50,
                ThreadPriority = ThreadPriority.BelowNormal
            };
            m_eventThreadPool = new SmartThreadPool(eventThreadPoolInfo);
            m_eventThreadPool.Start();

            m_running = true;
            Thread scriptEventsThread = new Thread(ScriptEventsThread);
            scriptEventsThread.Name = "XEngine (" + m_scene.Name + ")";
            scriptEventsThread.IsBackground = true;
            scriptEventsThread.Start();
        }

        public void Stop()
        {
            m_running = false;
        }

        public bool RezScript(UUID sourceItemID, UUID sourceAssetID, ISceneEntity hostObject, int scriptStartParam)
        {
            if (m_assetClient == null)
            {
                m_log.Error("Cannot rez script " + sourceItemID + " without an IAssetClient");
                return false;
            }

            Asset sourceAsset, binaryAsset;

            // Try to fetch the script source code asset
            if (m_assetClient.TryGetAsset(sourceAssetID, "application/vnd.ll.lsltext", out sourceAsset))
            {
                // The script binary assetID is the MD5 hash of the source to avoid lots of duplicate compiles
                UUID scriptBinaryAssetID = new UUID(Utils.MD5(sourceAsset.Data), 0);

                if (m_assetClient.TryGetAsset(scriptBinaryAssetID, "application/vnd.ll.lslbyte", out binaryAsset))
                {
                    m_log.Debug("Using existing compile for scriptID " + sourceItemID);

                    // Create an AppDomain for this script and post the entry point event to the queue
                    StartScript(sourceItemID, sourceAssetID, hostObject, binaryAsset.Data);
                }
                else
                {
                    #region Compile Script

                    ScriptCompiler converter = new ScriptCompiler(m_scene);
                    string csText =
                        "using System;\nusing System.Collections.Generic;\n" +
                        "using OpenMetaverse;\nusing Simian.Scripting.Linden;\n\n" +
                        "namespace SecondLife {\n    public class Script : LSLScriptBase {\n" +
                        converter.Convert(Encoding.UTF8.GetString(sourceAsset.Data)) +
                        "    }\n}\n";
                    string[] convertWarnings = converter.GetWarnings();

                    CompilerParameters parameters = new CompilerParameters();
                    parameters.IncludeDebugInformation = false;
                    parameters.CompilerOptions = "/optimize";
                    parameters.ReferencedAssemblies.Add("OpenMetaverseTypes.dll");
                    parameters.ReferencedAssemblies.Add("Simian.Scripting.Linden.dll");
                    parameters.GenerateExecutable = false;
                    parameters.GenerateInMemory = false;
                    parameters.TreatWarningsAsErrors = false;

                    CompilerResults results;
                    lock (m_csCodeProvider)
                        results = m_csCodeProvider.CompileAssemblyFromSource(parameters, csText);

                    #endregion Compile Script

                    if (convertWarnings.Length == 0 && !results.Errors.HasErrors)
                    {
                        #region Binary Storage

                        // Get the bytecode of the compiled assembly
                        byte[] scriptBinary = File.ReadAllBytes(results.PathToAssembly);
                        File.Delete(results.PathToAssembly);

                        // Save the assembly to the asset store
                        m_assetClient.StoreAsset(new Asset
                        {
                            ContentType = "application/vnd.ll.lslbyte",
                            CreatorID = hostObject.OwnerID,
                            Data = scriptBinary,
                            ID = scriptBinaryAssetID,
                            Local = true,
                            Temporary = true,
                            CreationDate = DateTime.UtcNow
                        });

                        #endregion Binary Storage

                        // Create an AppDomain for this script and post the entry point event to the queue
                        StartScript(sourceItemID, sourceAssetID, hostObject, scriptBinary);
                        return true;
                    }
                    else
                    {
                        #region Compile Error Handling

                        int displayErrors = 5;
                        string errtext = String.Empty;

                        foreach (string warning in convertWarnings)
                        {
                            // Show 5 errors max
                            if (displayErrors <= 0)
                                break;
                            --displayErrors;

                            // Treat converter warnings as errors
                            errtext += "Error: " + warning + "\n";
                        }

                        foreach (CompilerError compErr in results.Errors)
                        {
                            // Show 5 errors max
                            if (displayErrors <= 0)
                                break;
                            --displayErrors;

                            string severity = compErr.IsWarning ? "Warning" : "Error";
                            KeyValuePair<int, int> lslPos = FindErrorPosition(compErr.Line, compErr.Column, converter.PositionMap);
                            string text = compErr.ErrorText;

                            // Use LSL type names
                            text = ReplaceTypes(compErr.ErrorText);

                            // The Second Life viewer's script editor begins
                            // counting lines and columns at 0, so we subtract 1.
                            errtext += String.Format("Line ({0},{1}): {4} {2}: {3}\n",
                                    lslPos.Key - 1, lslPos.Value - 1,
                                    compErr.ErrorNumber, text, severity);
                        }

                        IScenePresence presence;
                        if (m_scene.TryGetPresence(hostObject.OwnerID, out presence))
                            m_scene.PresenceAlert(this, presence, "Script saved with warnings, check debug window!");

                        m_scene.EntityChat(this, hostObject, 0f, errtext, 0, EntityChatType.Debug);

                        #endregion Compile Error Handling
                    }
                }
            }
            else
            {
                m_log.Warn("Couldn't find script source asset " + sourceAssetID);
            }

            return false;
        }

        public void StopScript(UUID scriptID)
        {
            StopScript(scriptID, true);
        }

        private void StopScript(UUID scriptID, bool updatePrimFlags)
        {
            LSLScriptInstance instance;

            lock (m_syncRoot)
            {
                if (m_scripts.TryGetValue(scriptID, out instance))
                {
                    // If this script has a currently executing event, abort it
                    if (instance.CurrentExecution != null)
                    {
                        instance.CurrentExecution.Cancel(true);
                        instance.CurrentExecution = null;
                    }

                    // Tear down this AppDomain
                    AppDomain.Unload(instance.ScriptDomain);

                    // Remove the reference to this script from the scripts dictionary
                    m_scripts.Remove(scriptID);

                    // Remove the reference to this script from the entities dictionary
                    Dictionary<UUID, LSLScriptInstance> entityScripts;
                    if (m_scriptedEntities.TryGetValue(instance.Host.ID, out entityScripts))
                    {
                        entityScripts.Remove(scriptID);
                        if (entityScripts.Count == 0)
                            m_scriptedEntities.Remove(instance.Host.ID);
                    }

                    // Update the PrimFlags for the host object since it may no longer be 
                    // flagged Money, Touch, Scripted, etc
                    ISceneEntity host = instance.Host;
                    if (updatePrimFlags && host is LLPrimitive)
                        UpdatePrimFlags((LLPrimitive)host);
                }
            }
        }

        public bool IsScriptRunning(UUID scriptID)
        {
            lock (m_syncRoot)
                return m_scripts.ContainsKey(scriptID);
        }

        public bool PostScriptEvent(EventParams parms)
        {
            LSLScriptInstance instance;
            if (m_scripts.TryGetValue(parms.ScriptID, out instance))
            {
                if (parms.EventName == "timer")
                {
                    if (instance.IsTimerEventQueued)
                        return false;
                    else
                        instance.IsTimerEventQueued = true;
                }

                if (parms.EventName == "collision")
                {
                    if (instance.IsCollisionEventQueued)
                        return false;
                    else
                        instance.IsCollisionEventQueued = true;
                }

                if (parms.EventName == "control")
                {
                    int held = ((lsl_integer)parms.Params[1]).value;

                    // If the last message was a 0 (nothing held) and this one
                    // is also nothing held, drop it
                    if (held == 0 && instance.LastControlEventQueued == 0)
                        return false;

                    // If there is one or more control queued, then queue only
                    // changed controls, else queue unconditionally
                    if (instance.ControlEventsQueued > 0 && instance.LastControlEventQueued == held)
                        return false;

                    instance.LastControlEventQueued = held;
                    ++instance.ControlEventsQueued;
                }

                m_log.Debug("Posting script event " + parms.EventName + " to " + parms.ScriptID);
                instance.EnqueueEvent(parms);
                lock (m_syncRoot)
                    m_scriptsWithEvents.Enqueue(instance);

                return true;
            }

            return false;
        }

        public bool PostObjectEvent(UUID hostObjectID, string eventName, object[] eventParams, DetectParams[] detectParams)
        {
            bool ret = false;

            lock (m_syncRoot)
            {
                // See if we are tracking any scripts for this object
                Dictionary<UUID, LSLScriptInstance> entityScripts;
                if (m_scriptedEntities.TryGetValue(hostObjectID, out entityScripts))
                {
                    // If so, post the event to each of the scripts we are tracking in this object
                    foreach (LSLScriptInstance instance in entityScripts.Values)
                        ret |= PostScriptEvent(new EventParams(instance.ID, eventName, eventParams, detectParams));
                }
            }

            return ret;
        }

        public void SetTimerEvent(UUID scriptID, double seconds)
        {
            if (seconds == 0)
            {
                // Disabling timer
                UnsetTimerEvents(scriptID);
                return;
            }

            // Convert seconds to increments of 100 nanoseconds (ticks)
            ScriptTimer st = new ScriptTimer(scriptID, Convert.ToInt64(seconds * 10000000));
            // Adds if timer doesn't exist, otherwise replaces with new timer
            lock (m_syncRoot)
                m_scriptTimers[scriptID] = st;
        }

        public DetectParams GetDetectParams(UUID scriptID, int detectIndex)
        {
            return null;
        }

        public void SetStartParameter(UUID scriptID, int startParam)
        {
            LSLScriptInstance instance;

            lock (m_syncRoot)
            {
                if (m_scripts.TryGetValue(scriptID, out instance))
                    instance.StartParameter = startParam;
            }
        }

        public int GetStartParameter(UUID scriptID)
        {
            LSLScriptInstance instance;

            lock (m_syncRoot)
            {
                if (m_scripts.TryGetValue(scriptID, out instance))
                    return instance.StartParameter;
            }

            return 0;
        }

        public LSLEventFlags GetEventsForState(UUID scriptID, string state)
        {
            LSLScriptInstance instance;

            lock (m_syncRoot)
            {
                if (m_scripts.TryGetValue(scriptID, out instance))
                    return instance.GetEventsForState(state);
            }

            return 0;
        }

        public void SetScriptMinEventDelay(UUID scriptID, double minDelay)
        {
        }

        public void TriggerState(UUID scriptID, string newState)
        {
            // Lookup this script
            LSLScriptInstance instance;
            if (m_scripts.TryGetValue(scriptID, out instance))
            {
                if (newState == instance.State)
                    return;

                PostScriptEvent(new EventParams(instance.ID, "state_exit", new Object[0], new DetectParams[0]));
                PostScriptEvent(new EventParams(instance.ID, "state", new Object[] { newState }, new DetectParams[0]));
                PostScriptEvent(new EventParams(instance.ID, "state_entry", new Object[0], new DetectParams[0]));
            }
            else
            {
                m_log.Warn("ResetScript() called for unknown script " + scriptID);
            }
        }

        public void ApiResetScript(UUID scriptID)
        {
        }

        public void ResetScript(UUID scriptID)
        {
            // Lookup this script
            LSLScriptInstance instance;
            if (m_scripts.TryGetValue(scriptID, out instance))
            {
                // Start the script. This will stop it before starting the new instance
                RezScript(scriptID, instance.SourceAssetID, instance.Host, 0);
            }
            else
            {
                m_log.Warn("ResetScript() called for unknown script " + scriptID);
            }
        }

        public int AddListener(UUID scriptID, UUID hostObjectID, int channel, string name, UUID keyID, string message)
        {
            return 0;
        }

        public void RemoveListener(UUID scriptID, int handle)
        {
        }

        public void RemoveListeners(UUID scriptID)
        {
        }

        public void SetListenerState(UUID scriptID, int handle, bool enabled)
        {
        }

        public void SensorOnce(UUID scriptID, UUID hostObjectID, string name, UUID keyID, int type, double range, double arc)
        {
        }

        public void SensorRepeat(UUID scriptID, UUID hostObjectID, string name, UUID keyID, int type, double range, double arc, double rate)
        {
        }

        public void SensorRemove(UUID scriptID)
        {
        }

        private void StartScript(UUID sourceItemID, UUID sourceAssetID, ISceneEntity hostObject, byte[] scriptBinary)
        {
            // Create a new AppDomain for this script
            AppDomainSetup domainSetup = new AppDomainSetup();
            domainSetup.LoaderOptimization = LoaderOptimization.SingleDomain;
            AppDomain scriptDomain = AppDomain.CreateDomain(sourceItemID + ".lsl", null, domainSetup);

            // Create an instance (that lives in this AppDomain) and a wrapper
            // (that lives in the script AppDomain) for this script
            LSLScriptInstance instance = new LSLScriptInstance(sourceItemID, sourceAssetID, hostObject, scriptDomain);
            LSLScriptWrapper wrapper = (LSLScriptWrapper)scriptDomain.CreateInstanceAndUnwrap(
                Assembly.GetExecutingAssembly().FullName, "Simian.Scripting.Linden.LSLScriptWrapper");
            wrapper.Init(scriptBinary, instance);
            instance.Init(wrapper);

            lock (m_syncRoot)
            {
                // If this script is already running, shut it down
                StopScript(sourceItemID, false);

                // Keep track of this script
                m_scripts[sourceItemID] = instance;

                // Keep track of the entity containing this script
                Dictionary<UUID, LSLScriptInstance> entityScripts;
                if (!m_scriptedEntities.TryGetValue(hostObject.ID, out entityScripts))
                {
                    entityScripts = new Dictionary<UUID, LSLScriptInstance>();
                    m_scriptedEntities[hostObject.ID] = entityScripts;
                }
                entityScripts[instance.ID] = instance;
            }

            if (hostObject is LLPrimitive)
            {
                // Update the PrimFlags for the containing LLPrimitive
                LLPrimitive obj = (LLPrimitive)hostObject;
                bool hasCollisionEvents;

                PrimFlags oldFlags = obj.Prim.Flags;
                LSLEventFlags eventFlags = wrapper.GetEventsForState("default");

                PrimFlags newFlags = oldFlags;
                newFlags &= ~(PrimFlags.Touch | PrimFlags.Money);
                newFlags |= PrimFlags.Scripted | LSLEventFlagsToPrimFlags(eventFlags, out hasCollisionEvents);

                // FIXME: Do something with hasCollisionEvents

                // Either update the PrimFlags for this prim or just schedule 
                // it for serialization (since it has a new script)
                if (newFlags != oldFlags)
                {
                    obj.Prim.Flags = newFlags;
                    m_scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.PrimFlags);
                }
                else
                {
                    m_scene.EntityAddOrUpdate(this, obj, UpdateFlags.Serialize, 0);
                }
            }

            // Fire the state_entry event to get this script started
            PostScriptEvent(new EventParams(sourceItemID, "state_entry", new object[0], new DetectParams[0]));
        }

        private void UnsetTimerEvents(UUID scriptID)
        {
        }

        private void CheckTimerEvents()
        {
            if (m_scriptTimers.Count > 0)
            {
                lock (m_syncRoot)
                {
                    long now = DateTime.UtcNow.Ticks;

                    foreach (ScriptTimer st in m_scriptTimers.Values)
                    {
                        // Time has passed?
                        if (st.NextEvent < now)
                        {
                            // Add it to queue
                            PostScriptEvent(
                                    new EventParams(st.ScriptID, "timer", new object[0],
                                    new DetectParams[0]));

                            // Set next interval
                            st.NextEvent = now + st.TickInterval;
                        }
                    }
                }
            }
        }

        private void UpdatePrimFlags(LLPrimitive obj)
        {
            IList<LLInventoryTaskItem> scripts = obj.Inventory.GetScripts();
            LSLEventFlags eventFlags = 0;
            bool hasCollisionEvents;
            bool scripted = false;

            if (scripts.Count > 0)
            {
                scripted = true;

                // Aggregate LSLEventFlags for all of the running scripts in this prim
                lock (m_syncRoot)
                {
                    for (int i = 0; i < scripts.Count; i++)
                    {
                        LLInventoryTaskItem scriptItem = scripts[i];
                        LSLScriptInstance script;

                        if (m_scripts.TryGetValue(scriptItem.ID, out script))
                            eventFlags |= script.GetEventsForState(script.State);
                    }
                }
            }

            PrimFlags oldFlags = obj.Prim.Flags;

            PrimFlags newFlags = oldFlags;
            newFlags &= ~(PrimFlags.Scripted | PrimFlags.Touch | PrimFlags.Money);
            if (scripted) newFlags |= PrimFlags.Scripted;
            newFlags |= LSLEventFlagsToPrimFlags(eventFlags, out hasCollisionEvents);

            // FIXME: Do something with hasCollisionEvents

            if (newFlags != oldFlags)
            {
                obj.Prim.Flags = newFlags;
                m_scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.PrimFlags);
            }
        }

        private void SaveScriptState(LSLScriptInstance instance)
        {
            // Create a state file for this script
            byte[] scriptState = instance.GetSerializedState();

            m_assetClient.StoreAsset(new Asset
            {
                ContentType = "application/vnd.ll.lslstate",
                CreatorID = instance.Host.OwnerID,
                Data = scriptState,
                ID = UUID.Combine(instance.ID, SCRIPT_STATE_MAGIC_ID),
                Local = true,
                Temporary = true,
                CreationDate = DateTime.UtcNow
            });
        }

        private void ScriptEventsThread()
        {
            while (m_running)
            {
                // Check if there are free threads in the script thread pool
                if (m_eventThreadPool.MaxThreads - m_eventThreadPool.InUseThreads > 0)
                {
                    // Check HTTP requests

                    // Check XML-RPC requests

                    // Check listeners

                    // Check timers
                    CheckTimerEvents();

                    // Check sensors

                    // Check grid requests

                    // Dequeue the next script event
                    LSLScriptInstance instance = null;
                    EventParams scriptEvent = null;
                    lock (m_syncRoot)
                    {
                        // Try to dequeue the next script with a pending event,
                        // then the next pending event for that script
                        if (m_scriptsWithEvents.Count > 0)
                        {
                            instance = m_scriptsWithEvents.Dequeue();
                            if (instance != null)
                                scriptEvent = instance.DequeueEvent();
                        }
                    }

                    if (instance != null && scriptEvent != null)
                    {
                        if (scriptEvent.EventName == "timer")
                            instance.IsTimerEventQueued = false;
                        if (scriptEvent.EventName == "control")
                            instance.ControlEventsQueued--;
                        if (scriptEvent.EventName == "collision")
                            instance.IsCollisionEventQueued = false;

                        if (scriptEvent.EventName == "state")
                        {
                            // FIXME:
                            //instance.SetState(scriptEvent.EventName);
                            //LSLEventFlags flags = GetEventsForState(instance.ID, instance.State);
                        }
                        else
                        {
                            string methodName = instance.State + "_event_" + scriptEvent.EventName;

                            // Convert the method arguments to LSL types and queue 
                            // this event handler on the thread pool
                            ConvertParamsToLSLTypes(ref scriptEvent.Params);
                            instance.CurrentExecution = m_eventThreadPool.QueueWorkItem(RunScriptMethod, instance, methodName, scriptEvent.Params);
                        }
                    }
                }
                else
                {
                    m_log.Debug("No free threads in the script thread pool, sleeping...");
                }

                // TODO: Dynamic sleeping interval?
                Thread.Sleep(XENGINE_LONG_SLEEP_INTERVAL);

                m_scheduler.ThreadKeepAlive();
            }

            m_eventThreadPool.Dispose();
            m_scheduler.RemoveThread();
        }

        private void RunScriptMethod(LSLScriptInstance instance, string methodName, object[] parameters)
        {
            try
            {
                if (parameters != null && parameters.Length > 0)
                    instance.CallScriptMethod(methodName, parameters);
                else
                    instance.CallScriptMethod(methodName);
            }
            catch (Exception ex)
            {
                m_log.Error("Error executing method " + methodName + " in script " + instance.ID + ": " + ex);
            }

            lock (m_syncRoot)
            {
                // Mark this script as not having a running event
                instance.CurrentExecution = null;

                // Check if this script has more events. If so, add it back to 
                // the end of the queue
                if (instance.EventCount > 0)
                    m_scriptsWithEvents.Enqueue(instance);
            }
        }

        private static KeyValuePair<int, int> FindErrorPosition(int line, int col, Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> positionMap)
        {
            if (positionMap == null || positionMap.Count == 0)
                return new KeyValuePair<int, int>(line, col);

            KeyValuePair<int, int> ret = new KeyValuePair<int, int>();

            if (positionMap.TryGetValue(new KeyValuePair<int, int>(line, col), out ret))
                return ret;

            List<KeyValuePair<int, int>> sorted = new List<KeyValuePair<int, int>>(positionMap.Keys);

            sorted.Sort(KVP_SORTER);

            int l = 1;
            int c = 1;

            foreach (KeyValuePair<int, int> cspos in sorted)
            {
                if (cspos.Key >= line)
                {
                    if (cspos.Key > line)
                        return new KeyValuePair<int, int>(l, c);
                    if (cspos.Value > col)
                        return new KeyValuePair<int, int>(l, c);
                    c = cspos.Value;
                    if (c == 0)
                        c++;
                }
                else
                {
                    l = cspos.Key;
                }
            }

            return new KeyValuePair<int, int>(l, c);
        }

        private static string ReplaceTypes(string message)
        {
            message = message.Replace("lsl_string", "string");
            message = message.Replace("lsl_integer", "integer");
            message = message.Replace("lsl_float", "float");
            message = message.Replace("lsl_key", "key");
            message = message.Replace("lsl_list", "list");
            message = message.Replace("lsl_vector", "vector");
            message = message.Replace("lsl_rotation", "rotation");

            return message;
        }

        /// <summary>
        /// Converts .NET types to their LSL counterparts
        /// </summary>
        /// <param name="array">An array containing .NET type objects</param>
        private static void ConvertParamsToLSLTypes(ref object[] array)
        {
            if (array == null)
                return;

            for (int i = 0; i < array.Length; i++)
            {
                object obj = array[i];

                if (obj is Int32)
                    array[i] = new lsl_integer((int)obj);
                else if (obj is Single)
                    array[i] = new lsl_float((float)obj);
                else if (obj is Double)
                    array[i] = new lsl_float((double)obj);
                else if (obj is String)
                    array[i] = new lsl_string((string)obj);
                else if (obj is UUID)
                    array[i] = new lsl_key(((UUID)obj).ToString());
                else if (obj is Vector3)
                    array[i] = new lsl_vector((Vector3)obj);
                else if (obj is Quaternion)
                    array[i] = new lsl_rotation((Quaternion)obj);
            }
        }

        private static PrimFlags LSLEventFlagsToPrimFlags(LSLEventFlags eventFlags, out bool hasCollisionEvents)
        {
            PrimFlags flags = PrimFlags.None;

            if (eventFlags.HasFlag(LSLEventFlags.touch) || eventFlags.HasFlag(LSLEventFlags.touch_start) || eventFlags.HasFlag(LSLEventFlags.touch_end))
                flags |= PrimFlags.Touch;

            if (eventFlags.HasFlag(LSLEventFlags.money))
                flags |= PrimFlags.Money;

            if (eventFlags.HasFlag(LSLEventFlags.collision) || eventFlags.HasFlag(LSLEventFlags.collision_start) || eventFlags.HasFlag(LSLEventFlags.collision_end) ||
                eventFlags.HasFlag(LSLEventFlags.land_collision) || eventFlags.HasFlag(LSLEventFlags.land_collision_start) || eventFlags.HasFlag(LSLEventFlags.land_collision_end))
            {
                hasCollisionEvents = true;
            }
            else
            {
                hasCollisionEvents = false;
            }

            return flags;
        }
    }
}
