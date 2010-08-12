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
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse;

namespace Simian
{
    [ApplicationModule("FileDataStore")]
    public class FileDataStore : IDataStore, IApplicationModule
    {
        private const string DEFAULT_STORE_DIRECTORY = "LocalStore";
        private const string DEFAULT_TEMP_STORE_DIRECTORY = "LocalStore/Temporary";
        private const int DEFAULT_SERIALIZATION_INTERVAL = 30;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private Simian m_simian;
        private ThrottledQueue<string, SerializedData> m_pendingSerialization;
        private string m_storeDirectory;
        private string m_tempStoreDirectory;
        private string m_invalidPathCharsRegex;
        private System.Threading.ReaderWriterLockSlim m_rwLock = new System.Threading.ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        public bool Start(Simian simian)
        {
            m_simian = simian;

            int serializeSeconds = DEFAULT_SERIALIZATION_INTERVAL;

            // Regular expression to replace invalid path and filename characters
            m_invalidPathCharsRegex = "[" + Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars())) + "]";

            string executingDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            m_storeDirectory = Path.Combine(executingDir, DEFAULT_STORE_DIRECTORY);

            IConfig config = simian.Config.Configs["FileDataStore"];

            if (config != null)
            {
                string configPath = config.GetString("Path", DEFAULT_STORE_DIRECTORY);
                string tempPath = config.GetString("TempPath", DEFAULT_TEMP_STORE_DIRECTORY);
                serializeSeconds = config.GetInt("SaveInterval", DEFAULT_SERIALIZATION_INTERVAL);

                if (Path.IsPathRooted(configPath))
                    m_storeDirectory = configPath;
                else
                    m_storeDirectory = Path.Combine(executingDir, configPath);

                if (Path.IsPathRooted(tempPath))
                    m_tempStoreDirectory = tempPath;
                else
                    m_tempStoreDirectory = Path.Combine(executingDir, tempPath);
            }

            if (!Directory.Exists(m_storeDirectory))
            {
                try
                {
                    Directory.CreateDirectory(m_storeDirectory);
                }
                catch (Exception ex)
                {
                    m_log.Error("Failed to create local file store directory " + m_storeDirectory + ": " + ex.Message);
                    return false;
                }
            }

            m_log.Debug("Serialization interval set to " + serializeSeconds + " seconds");
            m_pendingSerialization = new ThrottledQueue<string, SerializedData>(0.1f, serializeSeconds * 1000, true, SerializeHandler);
            m_pendingSerialization.Start();
            return true;
        }

        public void Stop()
        {
            m_pendingSerialization.Stop(true);
        }

        public bool AddOrUpdateAsset(UUID dataID, string contentType, byte[] data)
        {
            string filename = GetFilename(dataID, contentType);
            return AddOrUpdate(filename, data);
        }

        public bool AddOrUpdateAsset(UUID dataID, string contentType, byte[] data, TimeSpan expiration)
        {
            // TODO: Store the expiration time
            string filename = GetTemporaryFilename(dataID, contentType);
            return AddOrUpdate(filename, data);
        }

        public bool RemoveAsset(UUID dataID, string contentType)
        {
            string temporaryFilename = GetTemporaryFilename(dataID, contentType);
            string filename = GetFilename(dataID, contentType);

            bool success = Remove(temporaryFilename);
            success |= Remove(filename);
            return success;
        }

        public bool TryGetAsset(UUID dataID, string contentType, out byte[] data)
        {
            string temporaryFilename = GetTemporaryFilename(dataID, contentType);
            string filename = GetFilename(dataID, contentType);

            m_rwLock.EnterReadLock();
            try
            {
                if (File.Exists(temporaryFilename))
                {
                    data = File.ReadAllBytes(temporaryFilename);
                    return true;
                }
                else if (File.Exists(filename))
                {
                    data = File.ReadAllBytes(filename);
                    return true;
                }
            }
            catch (Exception ex)
            {
                m_log.Error("Failed to fetch local data " + dataID + " (" + contentType + "): " + ex.Message);
            }
            finally
            {
                m_rwLock.ExitReadLock();
            }

            data = null;
            return false;
        }

        public IList<SerializedData> Deserialize(UUID storeID, string section)
        {
            string[] files = null;
            List<SerializedData> list = null;

            string dir = GetDirectory(storeID, section);
            if (Directory.Exists(dir))
            {
                try { files = Directory.GetFiles(dir); }
                catch { }
            }

            if (files != null)
            {
                list = new List<SerializedData>(files.Length);

                for (int i = 0; i < files.Length; i++)
                {
                    SerializedData data = CreateFromFile(files[i]);

                    if (data != null)
                        list.Add(data);
                }
            }
            else
            {
                list = new List<SerializedData>(0);
            }

            return list;
        }

        public SerializedData DeserializeOne(UUID storeID, string section)
        {
            string[] files = null;
            SerializedData latestData = null;

            string dir = GetDirectory(storeID, section);
            if (Directory.Exists(dir))
            {
                try { files = Directory.GetFiles(dir); }
                catch { }
            }

            if (files != null)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    SerializedData data = CreateFromFile(files[i]);

                    if (data != null)
                    {
                        if (latestData == null || data.Version > latestData.Version)
                            latestData = data;
                    }
                }
            }

            return latestData;
        }

        public void BeginSerialize(SerializedData item)
        {
            //m_log.Debug("Queuing serialization for " + GetFilename(item));
            m_pendingSerialization.Add(item.StoreID + item.Section + item.Name, item);
        }

        private bool AddOrUpdate(string filename, byte[] data)
        {
            m_rwLock.EnterWriteLock();
            try
            {
                if (File.Exists(filename))
                {
                    // Already exists
                    File.WriteAllBytes(filename, data);
                    return false;
                }
                else
                {
                    // New entry
                    CreateDirectories(filename);
                    File.WriteAllBytes(filename, data);
                    return true;
                }
            }
            catch (Exception ex)
            {
                m_log.Error("Failed to store local data to " + filename + ": " + ex.Message);
            }
            finally
            {
                m_rwLock.ExitWriteLock();
            }

            return false;
        }

        private bool Remove(string filename)
        {
            m_rwLock.EnterWriteLock();
            try
            {
                if (File.Exists(filename))
                {
                    File.Delete(filename);
                    return true;
                }
            }
            catch (Exception ex)
            {
                m_log.Error("Failed to delete local data file " + filename + ": " + ex.Message);
            }
            finally
            {
                m_rwLock.ExitWriteLock();
            }

            return false;
        }

        private void SerializeHandler(SerializedData data)
        {
            string filename = GetFilename(data);
            if (data.Data != null)
            {
                AddOrUpdate(filename, data.Data);
                m_log.Debug("Serialized " + data.Data.Length + " bytes to " + filename);
            }
            else if (Remove(filename))
            {
                m_log.Debug("Removed serialized data file " + filename);
            }
            else
            {
                m_log.Warn("Attempted to remove missing serialized data file " + filename);
            }
        }

        private SerializedData CreateFromFile(string filename)
        {
            try
            {
                SerializedData data = new SerializedData();

                DirectoryInfo dirInfo = new DirectoryInfo(Path.GetDirectoryName(filename));
                DirectoryInfo parentInfo = dirInfo.Parent;
                if (UUID.TryParse(parentInfo.Name, out data.StoreID))
                {
                    string shortFilename = Path.GetFileNameWithoutExtension(filename);

                    data.Section = dirInfo.Name;
                    data.ContentType = m_simian.ExtensionToContentType(Path.GetExtension(filename));
                    data.Version = 1;

                    int i = shortFilename.LastIndexOf("-v");
                    if (i > -1)
                    {
                        Int32.TryParse(shortFilename.Substring(i + 2), out data.Version);
                        data.Name = shortFilename.Substring(0, i);
                    }
                    else
                    {
                        data.Name = shortFilename;
                    }

                    data.Data = File.ReadAllBytes(filename);
                }

                return data;
            }
            catch (Exception ex)
            {
                m_log.Error("Failed to deserialize " + filename + ": " + ex.Message);
                return null;
            }
        }

        private string GetFilename(UUID dataID, string contentType)
        {
            string path = Regex.Replace(contentType, m_invalidPathCharsRegex, "_");
            path = Path.Combine(m_storeDirectory, path);
            path = Path.Combine(path, dataID.ToString());

            string extension = m_simian.ContentTypeToExtension(contentType);
            if (!String.IsNullOrEmpty(extension))
                path += '.' + extension;

            return path;
        }

        private string GetFilename(SerializedData data)
        {
            string path = Path.Combine(m_storeDirectory, data.StoreID.ToString());
            path = Path.Combine(path, data.Section);

            string extension = m_simian.ContentTypeToExtension(data.ContentType);
            if (!String.IsNullOrEmpty(extension))
                extension = '.' + extension;

            string filename;

            if (data.Version > 1)
                filename = String.Format("{0}-v{1:00}{2}", data.Name, data.Version, extension);
            else
                filename = String.Format("{0}{1}", data.Name, extension);

            return Path.Combine(path, filename);
        }

        private string GetDirectory(UUID storeID, string section)
        {
            string path = Path.Combine(m_storeDirectory, storeID.ToString());
            return Path.Combine(path, section);
        }

        private string GetTemporaryFilename(UUID dataID, string contentType)
        {
            string path = Regex.Replace(contentType, m_invalidPathCharsRegex, "_");
            path = Path.Combine(m_tempStoreDirectory, path);
            path = Path.Combine(path, dataID.ToString());

            string extension = m_simian.ContentTypeToExtension(contentType);
            if (!String.IsNullOrEmpty(extension))
                path += '.' + extension;

            return path;
        }

        private static void CreateDirectories(string filename)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(Path.GetDirectoryName(filename));
            CreateDirectory(dirInfo);
        }

        private static void CreateDirectory(DirectoryInfo dirInfo)
        {
            if (dirInfo.Parent != null && !dirInfo.Parent.Exists)
                CreateDirectory(dirInfo.Parent);

            if (!dirInfo.Exists)
                dirInfo.Create();
        }
    }
}
