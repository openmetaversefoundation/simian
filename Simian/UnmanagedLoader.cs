using System;
using System.Collections.Generic;
using System.IO;
using log4net;
using OpenMetaverse;

namespace Simian
{
    /// <summary>
    /// A helper class for dealing with unmanaged libraries
    /// </summary>
    public static class UnmanagedLoader
    {
        private const string LIB_EXTENSION_X86 = "-x86-32.dll";
        private const string LIB_EXTENSION_X64 = "-x86-64.dll";

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);
        private static Dictionary<string, bool> m_results = new Dictionary<string, bool>();

        /// <summary>
        /// Assuming a naming convention of libraryBaseName-x86-32.dll and 
        /// libraryBaseName-x86-64.dll, copies the appropriate library for the
        /// current platform to libraryBaseName.dll. This is a no-op on non-
        /// Windows platforms, where DllMap handles loading the appropriate 
        /// unmanaged library
        /// </summary>
        /// <param name="libraryBaseName">Base library name</param>
        /// <returns>True if the copy succeeded or was not needed, otherwise 
        /// false</returns>
        public static bool CopyLibrary(string libraryBaseName)
        {
            // Only do the .dll copy on Windows platforms
            Utils.Platform platform = Utils.GetRunningPlatform();
            if (platform != Utils.Platform.Windows && platform != Utils.Platform.WindowsCE)
                return true;

            bool result;

            // Check if we've already processed (or attempted to process) this library
            if (m_results.TryGetValue(libraryBaseName, out result))
                return result;

            string sourceLibrary = GetSourceName(libraryBaseName);
            string targetLibrary = libraryBaseName + ".dll";

            try
            {
                File.Copy(sourceLibrary, targetLibrary, true);
                m_log.Info("Copied library " + sourceLibrary + " to " + targetLibrary);
                result = true;
            }
            catch (IOException ex)
            {
                m_log.Error("Failed to copy source library " + sourceLibrary + " to " + targetLibrary + ": " + ex.Message);
                result = false;
            }

            m_results[libraryBaseName] = result;
            return result;
        }

        /// <summary>
        /// Test if we are executing in a 32-bit or 64-bit environment and 
        /// return libraryBaseName-x86-32.dll or libraryBaseName-x86-64.dll
        /// </summary>
        /// <param name="libraryBaseName">Base library name</param>
        /// <returns>The platform-specific library name</returns>
        private static string GetSourceName(string libraryBaseName)
        {
            if (IntPtr.Size == 4)
                return libraryBaseName + LIB_EXTENSION_X86;
            else
                return libraryBaseName + LIB_EXTENSION_X64;
        }
    }
}
