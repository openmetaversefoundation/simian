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
using HttpServer;
using Nwc.XmlRpc;
using OpenMetaverse;

namespace Simian
{
    /// <summary>
    /// Delegate for handling incoming HTTP requests
    /// </summary>
    /// <param name="context">Client context</param>
    /// <param name="request">HTTP request</param>
    /// <param name="response">HTTP response</param>
    public delegate void HttpRequestCallback(IHttpClientContext context, IHttpRequest request, IHttpResponse response);

    /// <summary>
    /// Callback for an XML-RPC request
    /// </summary>
    /// <param name="request">XML-RPC request data</param>
    /// <param name="httpRequest">Reference to the underlying HTTP request</param>
    /// <returns>XML-RPC response data</returns>
    public delegate XmlRpcResponse XmlRpcCallback(XmlRpcRequest request, IHttpRequest httpRequest);

    public interface IHttpServer
    {
        /// <summary>
        /// Base listening address of the HTTP server
        /// </summary>
        Uri HttpAddress { get; }

        /// <summary>
        /// Add a request handler
        /// </summary>
        /// <param name="method">HTTP verb to match, or null to skip verb matching</param>
        /// <param name="contentType">Content-Type header to match, or null to skip Content-Type matching</param>
        /// <param name="path">Request URI path to match, or null to skip URI path matching</param>
        /// <param name="exactPath">True to match the path exactly, false for partial path matching</param>
        /// <param name="sendResponseAfterCallback">True to send the HTTP response after the callback exits, or
        /// false to handle sending the response in the callback</param>
        /// <param name="callback">Callback to fire when an incoming request matches the given pattern</param>
        void AddHandler(string method, string contentType, string path, bool exactPath, bool sendResponseAfterCallback, HttpRequestCallback callback);

        /// <summary>
        /// Remove all request handlers using the given callback
        /// </summary>
        /// <param name="callback">Callback to unregister from the HTTP server</param>
        void RemoveHandlers(HttpRequestCallback callback);

        /// <summary>
        /// Set a callback to override the default 404 (Not Found) response
        /// </summary>
        /// <param name="callback">Callback that will be fired when an unhandled request is received, or null to
        /// reset to the default handler</param>
        void Set404Handler(HttpRequestCallback callback);

        /// <summary>
        /// Send a 404 (Not Found) response
        /// </summary>
        /// <param name="context">Client context</param>
        /// <param name="request">HTTP request</param>
        /// <param name="response">HTTP response</param>
        void Send404Response(IHttpClientContext context, IHttpRequest request, IHttpResponse response);

        /// <summary>
        /// Add an XML-RPC handler
        /// </summary>
        /// <param name="path">Request URI path to match, or null to skip URI path matching</param>
        /// <param name="exactPath">True to match the path exactly, otherwise false</param>
        /// <param name="methodName">XML-RPC method name to handle</param>
        /// <param name="callback">Callback to fire when an incoming request matches the given pattern</param>
        void AddXmlRpcHandler(string path, bool exactPath, string methodName, XmlRpcCallback callback);

        /// <summary>
        /// Remove all XML-RPC request handlers using the given callback
        /// </summary>
        /// <param name="methodName">Method name to unregister from the HTTP XML-RPC server</param>
        void RemoveXmlRpcHandlers(string methodName);
    }
}
