using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using log4net;
using Nini;
using Nini.Config;

namespace Simian
{
    /// <summary>
    /// Handles configuration file loading from disk and the web
    /// </summary>
    public class ConfigurationLoader
    {
        public const string SIMIAN_CONFIG_PATH = "Config";
        public const string SIMIAN_CONFIG_FILE = "SimianDefaults.ini";
        public const string SIMIAN_CONFIG_USER_FILE = "Simian.ini";
        public const string MIME_TYPE_CONFIG_FILE = "mime.types";

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private byte[] m_configData;
        private Dictionary<string, string> m_typesToExtensions = new Dictionary<string, string>();
        private Dictionary<string, string> m_extensionsToTypes = new Dictionary<string, string>();

        public Dictionary<string, string> TypesToExtensions { get { return m_typesToExtensions; } }
        public Dictionary<string, string> ExtensionsToTypes { get { return m_extensionsToTypes; } }

        public ConfigurationLoader(string masterConfigLocation, string overridesConfigLocation)
        {
            #region Overrides File Creation

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

            #endregion Overrides File Creation

            #region MIME Type File

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

            #endregion MIME Type File

            // Load the master ini file
            IniConfigSource masterConfig = LoadConfig(GetConfigPath(masterConfigLocation));

            // Load the overrides ini file
            IniConfigSource overridesConfig = LoadConfig(GetConfigPath(overridesConfigLocation));

            // Merge
            masterConfig.Merge(overridesConfig);

            // Save the merged config file in-memory so we can make copies later
            using (MemoryStream stream = new MemoryStream())
            {
                masterConfig.Save(stream);
                m_configData = stream.ToArray();
            }
        }

        public IConfigSource GetConfigCopy()
        {
            using (MemoryStream stream = new MemoryStream(m_configData))
                return new IniConfigSource(stream);
        }

        #region Configuration Helpers

        private static IniConfigSource LoadConfig(string location)
        {
            IniConfigSource currentConfig = new IniConfigSource();
            List<string> currentConfigLines = new List<string>();
            string[] configLines = null;

            if (IsUrl(location))
            {
                // Web-based loading
                string responseStr;
                if (WebUtil.TryGetUrl(location, out responseStr))
                {
                    configLines = responseStr.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    m_log.Error("Failed to load web config file " + location + ": " + responseStr);
                }
            }
            else
            {
                // Local file loading
                try
                {
                    configLines = new List<string>(File.ReadAllLines(location)).ToArray();
                }
                catch (Exception ex)
                {
                    m_log.Error("Failed to load config file " + location + ": " + ex.Message);
                }
            }

            if (configLines != null)
            {
                for (int i = 0; i < configLines.Length; i++)
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

                currentConfig.Merge(CompileConfig(currentConfigLines));
            }

            return currentConfig;
        }

        private static IniConfigSource CompileConfig(List<string> lines)
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

        private static string GetConfigPath(string location)
        {
            if (Path.IsPathRooted(location) || IsUrl(location))
            {
                return location;
            }
            else
            {
                string currentDir = Util.ExecutingDirectory();
                string configPath = Path.Combine(currentDir, SIMIAN_CONFIG_PATH);
                return Path.Combine(configPath, location);
            }
        }

        private static bool IsUrl(string file)
        {
            Uri configUri;
            return Uri.TryCreate(file, UriKind.Absolute, out configUri) &&
                (configUri.Scheme == Uri.UriSchemeHttp || configUri.Scheme == Uri.UriSchemeHttps);
        }

        #endregion Configuration Helpers
    }
}
