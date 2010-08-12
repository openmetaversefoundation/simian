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
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using HttpServer;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;

namespace Simian
{
    [ApplicationModule("HttpListener")]
    public class HttpListener : IHttpServer, IApplicationModule
    {
        private const int DEFAULT_HTTP_PORT = 12043;

        // XML-RPC error codes, from http://xmlrpc-epi.sourceforge.net/specs/rfc.fault_codes.php
        private const int METHOD_NOT_FOUND = -32601;
        private const int INTERNAL_ERROR = -32603;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private HttpServer.HttpListener m_httpServer;
        private X509Certificate2 m_sslCertificate;
        private Uri m_uri;
        private HttpRequestHandler[] m_requestHandlers = new HttpRequestHandler[0];
        private HttpRequestHandler m_notFoundHandler;
        private object m_handlersWriteLock = new object();

        private HashSet<string> m_xmlrpcPaths = new HashSet<string>();
        private Dictionary<string, XmlRpcCallback> m_xmlrpcCallbacks = new Dictionary<string, XmlRpcCallback>();
        private XmlRpcRequestDeserializer m_xmlrpcDeserializer = new XmlRpcRequestDeserializer();

        public Uri HttpAddress { get { return m_uri; } }

        public bool Start(Simian simian)
        {
            int port = DEFAULT_HTTP_PORT;
            string hostname = null;
            string sslCertFile = null;
            IPHostEntry entry;
            IPAddress address;

            // Create a logger for the HTTP server
            HttpLogWriter httpLogger = new HttpLogWriter(m_log);

            // Create a default 404 handler
            m_notFoundHandler = new HttpRequestHandler(null, Default404Handler, true);

            #region Config Variables

            IConfig config = simian.Config.Configs["HTTP"];

            if (config != null)
            {
                port = config.GetInt("ListenPort", DEFAULT_HTTP_PORT);
                hostname = config.GetString("Hostname", null);
                sslCertFile = config.GetString("SSLCertFile", null);
            }

            if (String.IsNullOrEmpty(hostname))
            {
                hostname = Dns.GetHostName();
                entry = Dns.GetHostEntry(hostname);
                address = IPAddress.Any;
            }
            else
            {
                entry = Dns.GetHostEntry(hostname);
                if (entry != null && entry.AddressList.Length > 0)
                {
                    address = entry.AddressList[0];
                }
                else
                {
                    m_log.Warn("Could not resolve an IP address from hostname " + hostname + ", binding to all interfaces");
                    address = IPAddress.Any;
                }
            }

            #endregion Config Variables

            #region Initialization

            if (!String.IsNullOrEmpty(sslCertFile))
            {
                // HTTPS mode
                try { m_sslCertificate = new X509Certificate2(sslCertFile); }
                catch (Exception ex)
                {
                    m_log.Error("Failed to load SSL certificate file \"" + sslCertFile + "\": " + ex.Message);
                    return false;
                }

                m_uri = new Uri("https://" + hostname + (port != 80 ? (":" + port) : String.Empty));
                m_httpServer = HttpServer.HttpListener.Create(address, port, m_sslCertificate, RemoteCertificateValidationHandler, SslProtocols.Default, false);
            }
            else
            {
                // HTTP mode
                m_uri = new Uri("http://" + hostname + (port != 80 ? (":" + port) : String.Empty));
                m_httpServer = HttpServer.HttpListener.Create(address, port);
            }

            m_httpServer.LogWriter = httpLogger;
            m_httpServer.RequestReceived += RequestReceivedHandler;

            m_httpServer.Start(64);
            m_log.Info("HTTP server is listening at " + m_uri);

            #endregion Initialization

            return true;
        }

        public void Stop()
        {
            if (m_httpServer != null)
            {
                m_httpServer.RequestReceived -= RequestReceivedHandler;
                m_httpServer.Stop();
            }
        }

        #region HTTP

        public void AddHandler(string method, string contentType, string path, bool exactPath, bool sendResponseAfterCallback, HttpRequestCallback callback)
        {
            HttpRequestSignature signature = new HttpRequestSignature(method, contentType, path, exactPath);
            HttpRequestHandler handler = new HttpRequestHandler(signature, callback, sendResponseAfterCallback);

            lock (m_handlersWriteLock)
            {
                HttpRequestHandler[] newHandlers = new HttpRequestHandler[m_requestHandlers.Length + 1];

                for (int i = 0; i < m_requestHandlers.Length; i++)
                    newHandlers[i] = m_requestHandlers[i];
                newHandlers[m_requestHandlers.Length] = handler;

                m_requestHandlers = newHandlers;
            }
        }

        public void RemoveHandlers(HttpRequestCallback callback)
        {
            lock (m_handlersWriteLock)
            {
                List<HttpRequestHandler> newHandlers = new List<HttpRequestHandler>(m_requestHandlers.Length - 1);

                for (int i = 0; i < m_requestHandlers.Length; i++)
                {
                    if (m_requestHandlers[i].Callback != callback)
                        newHandlers.Add(m_requestHandlers[i]);
                }

                m_requestHandlers = newHandlers.ToArray();
            }
        }

        public void Set404Handler(HttpRequestCallback callback)
        {
            m_notFoundHandler = new HttpRequestHandler(null, callback, true);
        }

        public void Send404Response(IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            m_notFoundHandler.Callback(context, request, response);
        }

        #endregion HTTP

        #region XML-RPC

        public void AddXmlRpcHandler(string path, bool exactPath, string methodName, XmlRpcCallback callback)
        {
            m_xmlrpcCallbacks[methodName] = callback;

            if (m_xmlrpcPaths.Add(path))
            {
                AddHandler("POST", "text/xml", path, exactPath, true, XmlRpcHandler);
                AddHandler("POST", "application/x-www-form-urlencoded", path, exactPath, true, XmlRpcHandler);
            }
        }

        public void RemoveXmlRpcHandlers(string methodName)
        {
            m_xmlrpcCallbacks.Remove(methodName);
        }

        #endregion XML-RPC

        private bool RemoteCertificateValidationHandler(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            m_log.Debug("Validating client SSL certificate. Errors: " + sslPolicyErrors);
            return true;
        }

        private void RequestReceivedHandler(object sender, RequestEventArgs e)
        {
            IHttpClientContext context = (IHttpClientContext)sender;
            IHttpRequest request = e.Request;

            IHttpResponse response = request.CreateResponse(context);

            // Load cookies if they exist
            RequestCookies cookies = (request.Headers["cookie"] != null)
                ? new RequestCookies(request.Headers["cookie"])
                : new RequestCookies(String.Empty);
            request.SetCookies(cookies);

            // Create a request signature
            HttpRequestSignature signature = new HttpRequestSignature(request);

            // Look for a signature match in our handlers
            HttpRequestHandler foundHandler = null;

            for (int i = 0; i < m_requestHandlers.Length; i++)
            {
                HttpRequestHandler handler = m_requestHandlers[i];

                if (signature == handler.Signature)
                {
                    foundHandler = handler;
                    break;
                }
            }

            if (foundHandler != null)
                FireRequestCallback(context, request, response, foundHandler);
            else
                FireRequestCallback(context, request, response, m_notFoundHandler);
        }

        private void FireRequestCallback(IHttpClientContext client, IHttpRequest request, IHttpResponse response, HttpRequestHandler handler)
        {
            try
            {
                handler.Callback(client, request, response);
            }
            catch (Exception ex)
            {
                m_log.Error("Exception in HTTP handler: " + ex);
                response.Status = HttpStatusCode.InternalServerError;
                response.Send();
            }

            if (handler.SendResponseAfterCallback && !response.Sent)
            {
                try { response.Send(); }
                catch (Exception ex) { m_log.ErrorFormat("Failed to send HTTP response for request to {0}: {1}", request.Uri, ex.Message); }
            }

            request.Clear();
        }

        private void XmlRpcHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            XmlRpcRequest rpcRequest = null;
            XmlRpcResponse rpcResponse = null;

            try
            { rpcRequest = m_xmlrpcDeserializer.Deserialize(new StreamReader(request.Body)) as XmlRpcRequest; }
            catch (SystemException ex)
            { m_log.Warn("Failed to deserialize incoming XML-RPC request: " + ex.Message); }

            if (rpcRequest != null)
            {
                response.ContentType = "text/xml";
                response.Encoding = Encoding.UTF8;
                response.Chunked = false;

                XmlRpcCallback callback;
                if (m_xmlrpcCallbacks.TryGetValue(rpcRequest.MethodName, out callback))
                {
                    // TODO: Add IHttpClientContext.RemoteEndPoint
                    rpcRequest.Params.Add(null); //rpcRequest.Params.Add(client.RemoteEndPoint);
                    rpcRequest.Params.Add(request.Uri);

                    try
                    {
                        rpcResponse = callback(rpcRequest, request);

                        string responseString = XmlRpcResponseSerializer.Singleton.Serialize(rpcResponse);
                        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                        // Set the content-length, otherwise the LL viewer freaks out
                        response.ContentLength = buffer.Length;
                        response.Body.Write(buffer, 0, buffer.Length);
                        response.Body.Flush();
                    }
                    catch (Exception ex)
                    {
                        m_log.ErrorFormat("XML-RPC method [{0}] threw exception: {1}", rpcRequest.MethodName, ex);

                        rpcResponse = new XmlRpcResponse();
                        rpcResponse.SetFault(INTERNAL_ERROR, String.Format("Requested method [{0}] threw exception: {1}", rpcRequest.MethodName, ex.Message));
                        XmlRpcResponseSerializer.Singleton.Serialize(new XmlTextWriter(response.Body, Encoding.UTF8), rpcResponse);
                    }
                }
                else
                {
                    m_log.WarnFormat("XML-RPC method [{0}] not found", rpcRequest.MethodName);

                    rpcResponse = new XmlRpcResponse();
                    rpcResponse.SetFault(METHOD_NOT_FOUND, String.Format("Requested method [{0}] not found", rpcRequest.MethodName));
                    XmlRpcResponseSerializer.Singleton.Serialize(new XmlTextWriter(response.Body, Encoding.UTF8), rpcResponse);
                }
            }
            else
            {
                m_log.Warn("Bad XML-RPC request");
                response.Status = HttpStatusCode.BadRequest;
            }
        }

        private void Default404Handler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            const string NOT_FOUND_RESPONSE = "<html><head><title>Page Not Found</title></head><body><h1>Page Not Found</h1></body></html>";

            m_log.Debug("Returning 404 for request to " + request.Uri);

            response.Status = HttpStatusCode.NotFound;
            byte[] buffer = System.Text.Encoding.ASCII.GetBytes(NOT_FOUND_RESPONSE);
            response.Body.Write(buffer, 0, buffer.Length);
        }
    }
}
