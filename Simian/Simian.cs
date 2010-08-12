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
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Timers;
using log4net;
using Nini.Config;
using OpenMetaverse;

namespace Simian
{
    public delegate void TaskCallback();
    public delegate void CommandCallback(string command, string[] args, bool printHelp);

    public class Simian
    {
        public const int LONG_SLEEP_INTERVAL = 200;

        const string SIMIAN_CONFIG_PATH = "Config";
        const string SIMIAN_CONFIG_FILE = "SimianDefaults.ini";
        const string SIMIAN_CONFIG_USER_FILE = "Simian.ini";
        const string MIME_TYPE_CONFIG_FILE = "mime.types";

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        #region Fields

        private CompositionContainer m_moduleContainer;
        private IConfigSource m_configSource;
        private IApplicationModule[] m_applicationModules;
        private ISceneFactory m_sceneFactory;
        private IHttpServer m_httpServer;
        private CapabilityRouter m_capabilityRouter;
        private Timer m_heartbeatTimer;

        private Dictionary<string, string> m_typesToExtensions = new Dictionary<string, string>();
        private Dictionary<string, string> m_extensionsToTypes = new Dictionary<string, string>();

        private Dictionary<string, CommandCallback> m_commandHandlers = new Dictionary<string, CommandCallback>();
        private IScene m_commandScene = null;

        /// <summary>The measured resolution of Environment.TickCount</summary>
        public readonly float TickCountResolution;

        #endregion Fields

        #region Properties

        public CompositionContainer ModuleContainer { get { return m_moduleContainer; } }
        public IConfigSource Config
        {
            get
            {
                if (m_configSource == null)
                    m_configSource = GetConfigCopy();

                return m_configSource;
            }
        }
        public CapabilityRouter Capabilities { get { return m_capabilityRouter; } }

        #endregion Properties

        #region Startup / Shutdown

        public Simian()
        {
            #region Environment.TickCount Measurement

            // Measure the resolution of Environment.TickCount
            TickCountResolution = 0f;
            for (int i = 0; i < 5; i++)
            {
                int start = Environment.TickCount;
                int now = start;
                while (now == start)
                    now = Environment.TickCount;
                TickCountResolution += (float)(now - start) * 0.2f;
            }
            m_log.Debug("Average Environment.TickCount resolution: " + TickCountResolution + "ms");
            TickCountResolution = (float)Math.Ceiling(TickCountResolution);

            #endregion Environment.TickCount Measurement

            #region Config Initialization

            // Create the user config include file if it doesn't exist to
            // prevent a warning message at startup
            try
            {
                string userConfigPath = GetConfigPath(SIMIAN_CONFIG_USER_FILE);
                if (!File.Exists(userConfigPath))
                {
                    using (FileStream stream = File.Create(userConfigPath))
                    { }
                }
            }
            catch { }

            string mimeConfigPath = GetConfigPath(MIME_TYPE_CONFIG_FILE);

            // Load and parse the MIME type file
            try
            {
                string[] mimeLines = File.ReadAllLines(mimeConfigPath);

                char[] splitChars = new char[] { ' ', '\t' };

                for (int i = 0; i < mimeLines.Length; i++)
                {
                    string line = mimeLines[i].Trim();

                    if (!String.IsNullOrEmpty(line) && line[0] != '#')
                    {
                        string[] parts = line.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                        {
                            string mimeType = parts[0];
                            m_typesToExtensions[mimeType] = parts[1];

                            for (int j = 1; j < parts.Length; j++)
                                m_extensionsToTypes[parts[j]] = mimeType;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.Error("Failed to load MIME types from  " + mimeConfigPath + ": " + ex.Message);
            }

            #endregion Config Initialization

            // Add a few commands
            AddCommandHandler("help", HelpHandler);
            AddCommandHandler("shutdown", ShutdownHandler);

            // Start the heartbeat timer, which fires once a second and allows modules to perform periodic tasks
            m_heartbeatTimer = new Timer(1000.0);
            m_heartbeatTimer.Elapsed += delegate{};
            m_heartbeatTimer.Start();
        }

        public void Shutdown()
        {
            m_heartbeatTimer.Stop();
            m_heartbeatTimer.Dispose();

            m_commandHandlers.Clear();

            // Shut down the scene factory first, to make sure all of the 
            // scenes shut down before the application modules
            IApplicationModule sceneFactory = GetAppModule<ISceneFactory>() as IApplicationModule;
            if (sceneFactory != null)
            {
                m_log.Info("Shutting down scene factory " + sceneFactory);
                sceneFactory.Stop();
            }

            foreach (IApplicationModule module in m_applicationModules)
            {
                if (!(module is ISceneFactory))
                {
                    m_log.Info("Shutting down application module " + module);
                    try
                    {
                        module.Stop();
                    }
                    catch (Exception ex)
                    {
                        m_log.Error("Error stopping application module " + module + ": " + ex.Message);
                    }
                }
            }
        }

        #endregion Startup / Shutdown

        #region Heartbeat Timer

        public void AddHeartbeatHandler(ElapsedEventHandler handler)
        {
            m_heartbeatTimer.Elapsed += handler;
        }

        public void RemoveHeartbeatHandler(ElapsedEventHandler handler)
        {
            m_heartbeatTimer.Elapsed -= handler;
        }

        #endregion Heartbeat Timer

        #region Module Handling

        public bool LoadModules()
        {
            #region Application Module Whitelist Loading

            // Load the application module whitelist
            List<string> whitelist = new List<string>();

            IConfig config = Config.Configs["ApplicationModules"];
            if (config != null)
            {
                foreach (string key in config.GetKeys())
                {
                    if (config.GetBoolean(key))
                        whitelist.Add(key);
                }
            }

            #endregion Application Module Whitelist Loading

            #region Module Container Loading

            AggregateCatalog catalog = new AggregateCatalog();

            AssemblyCatalog assemblyCatalog = new AssemblyCatalog(System.Reflection.Assembly.GetExecutingAssembly());
            DirectoryCatalog directoryCatalog = new DirectoryCatalog(".", "Simian.*.dll");

            catalog.Catalogs.Add(assemblyCatalog);
            catalog.Catalogs.Add(directoryCatalog);

            m_moduleContainer = new CompositionContainer(catalog, true);

            m_log.InfoFormat("Found {0} modules in the current assembly and {1} modules in external assemblies",
                assemblyCatalog.Parts.Count(), directoryCatalog.Parts.Count());

            #endregion Module Container Loading

            #region Module Loading

            IEnumerable<Lazy<object, object>> exportEnumerable = m_moduleContainer.GetExports(typeof(IApplicationModule), null, null);
            Dictionary<string, Lazy<object, object>> exports = new Dictionary<string, Lazy<object, object>>();
            List<IApplicationModule> imports = new List<IApplicationModule>();
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
                        m_log.Warn("Found an IApplicationModule with a duplicate name: " + name);
                }
            }

            // Load modules in the order they appear in the whitelist
            foreach (string whitelisted in whitelist)
            {
                Lazy<object, object> lazyExport;
                if (exports.TryGetValue(whitelisted, out lazyExport))
                {
                    imports.Add((IApplicationModule)lazyExport.Value);
                    exports.Remove(whitelisted);
                }
                else
                {
                    notLoaded.Add(whitelisted);
                }
            }

            // Populate m_applicationModules
            m_applicationModules = imports.ToArray();

            // Start the application modules
            for (int i = 0; i < m_applicationModules.Length; i++)
            {
                m_applicationModules[i].Start(this);

                // Get a reference to the IHttpServer if we have one
                if (m_applicationModules[i] is IHttpServer)
                {
                    // Create a global capability router
                    string capsPath = "/caps/";
                    m_httpServer = (IHttpServer)m_applicationModules[i];
                    m_capabilityRouter = new CapabilityRouter(m_httpServer.HttpAddress.Combine(capsPath));
                    m_httpServer.AddHandler(null, null, capsPath, false, false, m_capabilityRouter.RouteCapability);
                }
            }

            #endregion Module Loading

            #region Logging

            m_log.InfoFormat("Loaded {0} application modules", m_applicationModules.Length);

            if (exports.Count > 0)
            {
                StringBuilder skippedStr = new StringBuilder("Skipped application modules: ");
                foreach (string exportName in exports.Keys)
                    skippedStr.Append(exportName + " ");
                m_log.Info(skippedStr.ToString());
            }

            if (notLoaded.Count > 0)
            {
                StringBuilder notLoadedStr = new StringBuilder("Did not load whitelisted application modules: ");
                foreach (string entry in notLoaded)
                    notLoadedStr.Append(entry + " ");
                m_log.Warn(notLoadedStr.ToString());
            }

            #endregion Logging

            // Get a reference to the ISceneFactory if we have one
            m_sceneFactory = GetAppModule<ISceneFactory>();
            if (m_sceneFactory != null)
                AddCommandHandler("scene", SceneHandler);

            return true;
        }

        public T GetAppModule<T>()
        {
            // TODO: So slow!
            foreach (IApplicationModule module in m_applicationModules)
            {
                if (module is T)
                    return (T)module;
            }

            return default(T);
        }

        #endregion Module Handling

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

        public void HandleCommand(string command, string[] args, out string promptName)
        {
            if (!String.IsNullOrEmpty(command))
            {
                if (m_commandScene != null)
                {
                    // Pass commands to the current scene first
                    if (m_commandScene.HandleCommand(command, args))
                    {
                        // Check if the scene was shut down
                        if (!m_commandScene.IsRunning)
                            m_commandScene = null;

                        promptName = (m_commandScene != null) ? "simian:" + m_commandScene.Name : "simian";
                        return;
                    }
                }

                CommandCallback callback;
                lock (m_commandHandlers)
                    m_commandHandlers.TryGetValue(command, out callback);

                if (callback != null)
                    callback(command, args, false);
                else
                    Console.WriteLine(command + ": command not found");
            }

            promptName = (m_commandScene != null) ? "simian:" + m_commandScene.Name : "simian";
        }

        public string[] GetCompletions(string complete, out string prefix)
        {
            List<string> completions = new List<string>();
            bool emptyString = String.IsNullOrEmpty(complete);

            // Enable special tab completion for the help command
            if (complete.StartsWith("help "))
                complete = complete.Substring(5);

            int length = complete.Length;

            if (m_commandScene != null)
            {
                // Prepend commands from the current scene
                completions.AddRange(m_commandScene.GetCompletions(complete));
            }

            lock (m_commandHandlers)
            {
                foreach (string name in m_commandHandlers.Keys)
                {
                    if (emptyString || name.StartsWith(complete))
                    {
                        string completion = name.Substring(length);
                        if (!completions.Contains(completion))
                            completions.Add(completion);
                    }
                }
            }

            prefix = complete;
            return completions.ToArray();
        }

        private void HelpHandler(string command, string[] args, bool printHelp)
        {
            if (printHelp || args.Length == 0)
            {
                Console.WriteLine("Show help information for a command");
            }
            else
            {
                string helpCommand = args[0];

                if (m_commandScene != null)
                {
                    // Try to print help from the current scene first
                    string[] newArgs;
                    if (args.Length > 0)
                    {
                        newArgs = new string[args.Length - 1];
                        for (int i = 0; i < newArgs.Length; i++)
                            newArgs[i] = args[i + 1];
                    }
                    else
                    {
                        newArgs = args;
                    }

                    if (m_commandScene.HelpHandler(helpCommand, newArgs))
                        return;
                }

                // Try to print help from the callbacks registered with Simian
                CommandCallback callback;
                lock (m_commandHandlers)
                    m_commandHandlers.TryGetValue(helpCommand, out callback);

                if (callback != null)
                    callback(helpCommand, args, true);
                else
                    Console.WriteLine("-help: " + helpCommand + ": command not found");
            }
        }

        private void ShutdownHandler(string command, string[] args, bool printHelp)
        {
            // TODO: Implement the command line options of the Linux shutdown command,
            // including sending a message to all connected presences

            if (printHelp)
            {
                Console.WriteLine("Bring Simian down");
            }
            else
            {
                Shutdown();
                Environment.Exit(0);
            }
        }

        private void SceneHandler(string command, string[] args, bool printHelp)
        {
            if (printHelp)
            {
                Console.WriteLine("Set or unset the scene that commands will be sent to. " +
                    "Use the scene command without any arguments to unset the current scene.\n\n" +
                    "Examples:\nscene\nscene Hello World");
            }
            else if (args.Length == 0)
            {
                // Unsetting current scene
                m_commandScene = null;
                Console.WriteLine("Commands will be sent to Simian directly");
            }
            else
            {
                string sceneName = String.Join(" ", args).Replace("\"", String.Empty);

                IScene[] scenes = m_sceneFactory.GetScenes();
                foreach (IScene scene in scenes)
                {
                    if (scene.Name.Equals(sceneName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (scene.IsRunning)
                        {
                            m_commandScene = scene;
                            Console.WriteLine("Commands will be sent to the \"{0}\" scene", m_commandScene.Name);
                        }
                        else
                        {
                            Console.WriteLine("Scene \"{0}\" is not running", scene.Name);
                        }

                        return;
                    }
                }

                Console.WriteLine("-scene: " + sceneName + ": scene not found");
                Console.WriteLine();

                StringBuilder output = new StringBuilder("Available Scenes:\n");

                foreach (IScene scene in scenes)
                {
                    if (scene.IsRunning)
                        output.AppendLine(' ' + scene.Name);
                }

                Console.WriteLine(output.ToString());
            }
        }

        #endregion Command Handling

        #region MIME Type / File Extension Conversion

        public string ContentTypeToExtension(string contentType)
        {
            string extension;
            if (!String.IsNullOrEmpty(contentType) && m_typesToExtensions.TryGetValue(contentType, out extension))
                return extension;
            else
                return null;
        }

        public string ExtensionToContentType(string extension)
        {
            string contentType;
            if (m_extensionsToTypes.TryGetValue(extension, out contentType))
                return contentType;
            else
                return "application/octet-stream";
        }

        #endregion MIME Type / File Extension Conversion

        #region Configuration Helpers

        public IConfigSource GetConfigCopy()
        {
            return LoadConfig(GetConfigPath(SIMIAN_CONFIG_FILE));
        }

        private IniConfigSource LoadConfig(string filename)
        {
            IniConfigSource currentConfig = new IniConfigSource();
            List<string> currentConfigLines = new List<string>();

            try
            {
                List<string> configLines = new List<string>(File.ReadAllLines(filename));

                for (int i = 0; i < configLines.Count; i++)
                {
                    string line = configLines[i].Trim();

                    if (line.StartsWith("Include "))
                    {
                        // Compile the current config lines, compile the included config file, and combine them
                        currentConfig.Merge(CompileConfig(currentConfigLines));
                        currentConfigLines.Clear();

                        // Compile the included config file
                        string includeFilename = GetConfigPath(line.Substring(8).Trim());
                        IniConfigSource includeConfig = LoadConfig(includeFilename);

                        // Merge the included config with the curent config
                        currentConfig.Merge(includeConfig);
                    }
                    else if (!String.IsNullOrEmpty(line) && !line.StartsWith(";"))
                    {
                        currentConfigLines.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.Error("Failed to load config file " + filename + ": " + ex.Message);
            }

            currentConfig.Merge(CompileConfig(currentConfigLines));
            return currentConfig;
        }

        private IniConfigSource CompileConfig(List<string> lines)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    byte[] line = Encoding.UTF8.GetBytes(lines[i]);
                    stream.Write(line, 0, line.Length);
                    stream.WriteByte(0x0A); // Linefeed
                }

                stream.Seek(0, SeekOrigin.Begin);
                return new IniConfigSource(stream);
            }
        }

        private string GetConfigPath(string filename)
        {
            if (Path.IsPathRooted(filename))
            {
                return filename;
            }
            else
            {
                string currentDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string configPath = Path.Combine(currentDir, SIMIAN_CONFIG_PATH);
                return Path.Combine(configPath, filename);
            }
        }

        #endregion Configuration Helpers
    }
}
