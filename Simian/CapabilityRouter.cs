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
using HttpServer;
using log4net;
using OpenMetaverse;

namespace Simian
{
    /// <summary>
    /// Delegate for handling incoming capabilities
    /// </summary>
    /// <param name="capability">Capability this request was routed through</param>
    /// <param name="context">Client context</param>
    /// <param name="request">HTTP request</param>
    /// <param name="response">HTTP response</param>
    public delegate void CapabilityCallback(Capability capability, IHttpClientContext context, IHttpRequest request, IHttpResponse response);

    /// <summary>
    /// Routes from capability IDs to protected resources. Protected resources
    /// may be local callbacks or remote URLs
    /// </summary>
    public class CapabilityRouter
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        /// <summary>The base URL for capabilities</summary>
        private string m_capBaseUrl;
        /// <summary>Maps capabilityIDs to capabilities</summary>
        private Dictionary<UUID, Capability> m_capabilities = new Dictionary<UUID, Capability>();
        /// <summary>Maps ownerIDs to all of the capabilities owned by each
        /// ownerID. Used to look up existing capabilities by name, or quickly 
        /// destroy all capabilities associated with an owner</summary>
        private Dictionary<UUID, Dictionary<string, Capability>> m_ownerCapabilities = new Dictionary<UUID, Dictionary<string, Capability>>();
        /// <summary>Maps protected resource locations to the actual callbacks</summary>
        private Dictionary<string, CapabilityCallback> m_protectedResources = new Dictionary<string, CapabilityCallback>();
        /// <summary>Provides thread safety and synchronization between the 
        /// various collections in this class</summary>
        private System.Threading.ReaderWriterLockSlim m_capSyncRoot = new System.Threading.ReaderWriterLockSlim(System.Threading.LockRecursionPolicy.NoRecursion);

        /// <summary>The base URL for capabilities</summary>
        public Uri CapabilityBaseAddress { get { return new Uri(m_capBaseUrl); } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="baseUrl">Absolute base URL for capabilities, such as http://www.myserver.com/caps/</param>
        public CapabilityRouter(Uri baseUrl)
        {
            if (!baseUrl.IsAbsoluteUri)
                throw new ArgumentException("baseUrl must be an absolute URL", "baseUrl");

            // HACK: Some viewers have problems resolving local hostnames.
            // Force local hostnames to an IP address
            string host = baseUrl.DnsSafeHost;
            if (!host.Contains("."))
                host = Utils.HostnameToIPv4(baseUrl.DnsSafeHost).ToString();
            m_capBaseUrl = baseUrl.Scheme + "://" + host + ":" + baseUrl.Port + baseUrl.PathAndQuery;
        }

        /// <summary>
        /// Registers a mapping from a named resource to an internal method
        /// </summary>
        /// <param name="resourceOwnerID">Owner of the protected resource, such
        /// as a scene ID</param>
        /// <param name="resource">Protected resource name</param>
        /// <param name="callback">The protected resource</param>
        public void AddProtectedResource(UUID resourceOwnerID, string resource, CapabilityCallback callback)
        {
            resource = resourceOwnerID.ToString() + "/" + resource;

            m_capSyncRoot.EnterWriteLock();
            try
            {
                if (m_protectedResources.ContainsKey(resource))
                    m_log.Warn("Overwriting protected resource " + resource + " with new callback in " + callback.Target);
                m_protectedResources[resource] = callback;
            }
            finally { m_capSyncRoot.ExitWriteLock(); }
        }

        /// <summary>
        /// Unregisters a mapping from a named resource to an internal method
        /// </summary>
        /// <param name="resourceOwnerID">Owner of the protected resource, such
        /// as a scene ID</param>
        /// <param name="resource">Address of the protected resource to
        /// unregister</param>
        public bool RemoveProtectedResource(UUID resourceOwnerID, string resource)
        {
            resource = resourceOwnerID.ToString() + "/" + resource;

            m_capSyncRoot.EnterWriteLock();
            try { return m_protectedResources.Remove(resource); }
            finally { m_capSyncRoot.ExitWriteLock(); }
        }

        /// <summary>
        /// Create a capability mapping to a protected resource
        /// </summary>
        /// <param name="ownerID">Capability owner</param>
        /// <param name="sendResponseAfterCallback">Set this to false to leave 
        /// the connection open after the capability has been routed. Useful 
        /// for event queue capabilities</param>
        /// <param name="resourceOwnerID">Owner of the protected resource, such
        /// as a scene ID</param>
        /// <param name="resource">Protected resource to map to</param>
        /// <returns>Absolute URL of the capability</returns>
        public Uri AddCapability(UUID ownerID, bool sendResponseAfterCallback, UUID resourceOwnerID, string resource)
        {
            resource = resourceOwnerID.ToString() + "/" + resource;

            Capability cap = null;
            Dictionary<string, Capability> ownerCaps;

            m_capSyncRoot.EnterWriteLock();
            try
            {
                // Check if this ownerID has any capabilities yet
                if (!m_ownerCapabilities.TryGetValue(ownerID, out ownerCaps))
                {
                    ownerCaps = new Dictionary<string, Capability>();
                    m_ownerCapabilities[ownerID] = ownerCaps;
                }

                if (!ownerCaps.TryGetValue(resource, out cap))
                {
                    // Capability doesn't exist yet, create it
                    cap = new Capability(UUID.Random(), ownerID, resource, sendResponseAfterCallback);

                    // Add this capability to the capabilities collection
                    m_capabilities[cap.ID] = cap;

                    // Add this capability to the list of capabilities owned by ownerID
                    ownerCaps[resource] = cap;
                }
            }
            finally { m_capSyncRoot.ExitWriteLock(); }

            return new Uri(m_capBaseUrl + cap.ID.ToString(), UriKind.Absolute);
        }

        /// <summary>
        /// Create a one-time capability mapping to a protected callback
        /// resource
        /// </summary>
        /// <param name="ownerID">Capability owner</param>
        /// <param name="sendResponseAfterCallback">Set this to false to leave 
        /// the connection open after the capability has been routed. Useful 
        /// for event queue capabilities</param>
        /// <param name="resource">Protected one-time resource to map to</param>
        /// <returns>Absolute URL of the capability</returns>
        public Uri AddOneTimeCapability(UUID ownerID, bool sendResponseAfterCallback, CapabilityCallback resource)
        {
            Capability cap = null;
            Dictionary<string, Capability> ownerCaps;

            m_capSyncRoot.EnterWriteLock();
            try
            {
                // Check if this ownerID has any capabilities yet
                if (!m_ownerCapabilities.TryGetValue(ownerID, out ownerCaps))
                {
                    ownerCaps = new Dictionary<string, Capability>();
                    m_ownerCapabilities[ownerID] = ownerCaps;
                }

                // Create the one-time capability
                cap = new Capability(UUID.Random(), ownerID, resource, sendResponseAfterCallback);

                // Add this capability to the capabilities collection
                m_capabilities[cap.ID] = cap;

                // Add this capability to the list of capabilities owned by ownerID
                ownerCaps[cap.Resource] = cap;
            }
            finally { m_capSyncRoot.ExitWriteLock(); }

            return new Uri(m_capBaseUrl + cap.ID.ToString(), UriKind.Absolute);
        }

        /// <summary>
        /// Remove a single capability
        /// </summary>
        /// <param name="capabilityID">ID of the capability to remove</param>
        /// <returns>True if the capability was found and removed, otherwise
        /// false</returns>
        public bool RemoveCapability(UUID capabilityID)
        {
            m_capSyncRoot.EnterWriteLock();
            try
            {
                Capability cap;
                if (m_capabilities.TryGetValue(capabilityID, out cap))
                {
                    m_log.Debug("Removing capability " + capabilityID + " for " + cap.OwnerID + " mapping to " + cap.ResourceDisplayName);
                    m_capabilities.Remove(capabilityID);

                    Dictionary<string, Capability> ownerCaps;
                    if (m_ownerCapabilities.TryGetValue(cap.OwnerID, out ownerCaps))
                        ownerCaps.Remove(cap.Resource);

                    return true;
                }
            }
            finally { m_capSyncRoot.ExitWriteLock(); }

            return false;
        }

        /// <summary>
        /// Remove all of the capabilities associated with an ownerID
        /// </summary>
        /// <param name="ownerID">All capabilities associated with this ownerID
        /// will be removed</param>
        /// <returns>True if any capabilities were removed, otherwise false</returns>
        public bool RemoveCapabilities(UUID ownerID)
        {
            m_capSyncRoot.EnterWriteLock();
            try
            {
                m_log.Debug("Removing all capabilities for " + ownerID);

                Dictionary<string, Capability> ownerCaps;
                if (m_ownerCapabilities.TryGetValue(ownerID, out ownerCaps))
                {
                    foreach (Capability cap in ownerCaps.Values)
                        m_capabilities.Remove(cap.ID);

                    m_ownerCapabilities.Remove(ownerID);

                    return true;
                }
            }
            finally { m_capSyncRoot.ExitWriteLock(); }

            return false;
        }

        /// <summary>
        /// Checks a list of requested capabilities and the current registered
        /// protected resources for a resource name. If the resource is found
        /// in both places, a capability is assigned for the resource
        /// </summary>
        /// <param name="ownerID">Owner of the new capability</param>
        /// <param name="capabilities">Collection of capability requests</param>
        /// <param name="sendResponseAfterCallback">Set this to false to leave 
        /// the connection open after the capability has been routed. Useful 
        /// for event queue capabilities</param>
        /// <param name="resourceOwnerID">Owner of the protected resource, such
        /// as a scene ID</param>
        /// <param name="resource">The protected resource to create a 
        /// capability for</param>
        /// <returns>True if a capability was assigned, otherwise false</returns>
        public bool TryAssignCapability(UUID ownerID, IDictionary<string, Uri> capabilities, bool sendResponseAfterCallback, UUID resourceOwnerID, string resource)
        {
            return TryAssignCapability(ownerID, capabilities, sendResponseAfterCallback, resourceOwnerID, resource, resource);
        }

        /// <summary>
        /// Checks a list of requested capabilities and the current registered
        /// protected resources for a resource name. If the resource is found
        /// in both places, a capability is assigned for the resource
        /// </summary>
        /// <param name="ownerID">Owner of the new capability</param>
        /// <param name="capabilities">Collection of capability requests</param>
        /// <param name="sendResponseAfterCallback">Set this to false to leave 
        /// the connection open after the capability has been routed. Useful 
        /// for event queue capabilities</param>
        /// <param name="resourceOwnerID">Owner of the protected resource, such
        /// as a scene ID</param>
        /// <param name="resource">The protected resource to create a 
        /// capability for</param>
        /// <param name="alias">The name of the capability in the request</param>
        /// <returns>True if a capability was assigned, otherwise false</returns>
        public bool TryAssignCapability(UUID ownerID, IDictionary<string, Uri> capabilities, bool sendResponseAfterCallback, UUID resourceOwnerID, string resource, string alias)
        {
            // TODO: Need special handling for absolute URL (remote) resources

            // Check if this resource was requested and not already assigned
            Uri existingCap;
            if (!capabilities.TryGetValue(alias, out existingCap) || existingCap != null)
                return false;

            // Check if this resource exists locally
            m_capSyncRoot.EnterReadLock();
            try
            {
                if (!m_protectedResources.ContainsKey(resourceOwnerID.ToString() + "/" + resource))
                    return false;
            }
            finally { m_capSyncRoot.ExitReadLock(); }

            // Create the capability
            capabilities[alias] = AddCapability(ownerID, sendResponseAfterCallback, resourceOwnerID, resource);
            return true;
        }

        /// <summary>
        /// Routes an incoming HTTP capability request to an internal method or a remote resource
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <param name="request">HTTP request</param>
        /// <param name="response">HTTP response</param>
        public void RouteCapability(IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            UUID capabilityID;
            string path = request.Uri.AbsolutePath.TrimEnd('/');

            if (UUID.TryParse(path.Substring(path.Length - 36), out capabilityID))
            {
                Capability cap = null;
                CapabilityCallback callback = null;

                m_capSyncRoot.EnterReadLock();
                try
                {
                    if (m_capabilities.TryGetValue(capabilityID, out cap))
                    {
                        if (cap.OneTimeResource != null)
                            callback = cap.OneTimeResource;
                        else
                            m_protectedResources.TryGetValue(cap.Resource, out callback);
                    }
                }
                finally { m_capSyncRoot.ExitReadLock(); }

                if (cap != null)
                {
                    if (callback != null)
                    {
                        RouteLocalCapability(cap, callback, context, request, response);

                        if (cap.OneTimeResource != null)
                        {
                            // This was a one time resource, destroy it
                            RemoveCapability(cap.ID);
                        }

                        return;
                    }
                    else if (cap.Resource.StartsWith("https://") || cap.Resource.StartsWith("http://"))
                    {
                        RouteRemoteCapability(cap, context, request, response);
                        return;
                    }
                    else
                    {
                        m_log.Warn("Capability " + cap.ID + " owned by " + cap.OwnerID + " maps to missing resource " + cap.ResourceDisplayName);
                    }
                }
            }

            // Return a 404
            m_log.Warn("Returning 404 for capability request to " + request.Uri);
            response.Status = System.Net.HttpStatusCode.NotFound;
            try { response.Send(); }
            catch (Exception ex) { m_log.ErrorFormat("Failed to send HTTP response for request to (missing) capability {0}: {1}", request.Uri, ex.Message); }
        }

        private void RouteLocalCapability(Capability cap, CapabilityCallback callback, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            callback(cap, context, request, response);

            if (cap.SendResponseAfterCallback && !response.Sent)
            {
                try { response.Send(); }
                catch (Exception ex) { m_log.ErrorFormat("Failed to send HTTP response for request to capability {0}: {1}", request.Uri, ex.Message); }
            }
        }

        private void RouteRemoteCapability(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            // TODO: Proxy IHttpRequest to a new HttpWebRequest pointing at cap.Resource
            throw new NotImplementedException();
        }
    }
}
