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
using System.Text.RegularExpressions;
using HttpServer;

namespace Simian
{
    /// <summary>
    /// Used to match incoming HTTP requests against registered handlers.
    /// Matches based on any combination of HTTP Method, Content-Type header,
    /// and URL path. URL path matching supports the * wildcard character
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{method} {path} Content-Type: {contentType}")]
    public sealed class HttpRequestSignature : IEquatable<HttpRequestSignature>
    {
        private string method;
        private string contentType;
        private string path;
        private bool exactPath;

        /// <summary>
        /// Builds an HttpRequestSignature from passed in parameters
        /// </summary>
        /// <param name="method">HTTP method to match, or null to skip</param>
        /// <param name="contentType">HTTP Content-Type header to match, or null to skip</param>
        /// <param name="path">String to match against the exact or beginning URL path and query</param>
        /// <param name="exactPath">True to do exact path matching, otherwise false</param>
        public HttpRequestSignature(string method, string contentType, string path, bool exactPath)
        {
            this.method = (method != null) ? method.ToUpperInvariant() : String.Empty;
            this.contentType = (contentType != null) ? contentType.ToLowerInvariant() : String.Empty;
            this.path = path ?? String.Empty;
            this.exactPath = exactPath;
        }

        /// <summary>
        /// Builds an HttpRequestSignature from an incoming request
        /// </summary>
        /// <param name="request">Incoming request to build the signature from</param>
        public HttpRequestSignature(IHttpRequest request)
        {
            this.method = request.Method.ToUpperInvariant();
            this.contentType = request.Headers.Get("content-type");
            this.contentType = (this.contentType != null) ? this.contentType.ToLowerInvariant() : String.Empty;
            this.path = request.Uri.PathAndQuery;
            this.exactPath = true;
        }

        /// <summary>
        /// Test if two HTTP request signatures contain exactly the same data
        /// </summary>
        /// <param name="signature">Signature to test against</param>
        /// <returns>True if the contents of both signatures are identical, 
        /// otherwise false</returns>
        public bool ExactlyEquals(HttpRequestSignature signature)
        {
            return method.Equals(signature.method) &&
                contentType.Equals(signature.contentType) &&
                path.Equals(signature.path) &&
                exactPath == signature.exactPath;
        }

        /// <summary>
        /// Does pattern matching to determine if an incoming HTTP request
        /// matches a given pattern. Equals can only be called on an incoming
        /// request; the pattern to match against is the parameter
        /// </summary>
        /// <param name="obj">The pattern to test against this request</param>
        /// <returns>True if the request matches the given pattern, otherwise
        /// false</returns>
        public override bool Equals(object obj)
        {
            return (obj is HttpRequestSignature) ? this == (HttpRequestSignature)obj : false;
        }

        /// <summary>
        /// Does pattern matching to determine if an incoming HTTP request
        /// matches a given pattern. Equals can only be called on an incoming
        /// request; the pattern to match against is the parameter
        /// </summary>
        /// <param name="pattern">The pattern to test against this request</param>
        /// <returns>True if the request matches the given pattern, otherwise
        /// false</returns>
        public bool Equals(HttpRequestSignature pattern)
        {
            return (pattern != null && this == pattern);
        }

        /// <summary>
        /// Returns the hash code for this signature
        /// </summary>
        /// <returns>Hash code for this signature</returns>
        public override int GetHashCode()
        {
            return method.GetHashCode() ^ contentType.GetHashCode() ^ path.GetHashCode();
        }

        /// <summary>
        /// Does pattern matching to determine if an incoming HTTP request
        /// matches a given pattern. The incoming request must be on the
        /// left-hand side, and the pattern to match against must be on the
        /// right-hand side
        /// </summary>
        /// <param name="request">The incoming HTTP request signature</param>
        /// <param name="pattern">The pattern to test against the incoming request</param>
        /// <returns>True if the request matches the given pattern, otherwise
        /// false</returns>
        public static bool operator ==(HttpRequestSignature request, HttpRequestSignature pattern)
        {
            // Compare HTTP method
            if (!String.IsNullOrEmpty(pattern.method) && request.method != pattern.method)
                return false;

            // Compare Content-Type header
            if (!String.IsNullOrEmpty(pattern.contentType) && request.contentType != pattern.contentType)
                return false;

            // Compare path
            if (!String.IsNullOrEmpty(pattern.path))
            {
                if (pattern.exactPath)
                    return request.path.Equals(pattern.path, StringComparison.InvariantCultureIgnoreCase);
                else
                    return request.path.StartsWith(pattern.path, StringComparison.InvariantCultureIgnoreCase);
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Does pattern matching to determine if an incoming HTTP request
        /// matches a given pattern. The incoming request must be on the
        /// left-hand side, and the pattern to match against must be on the
        /// right-hand side
        /// </summary>
        /// <param name="request">The incoming HTTP request signature</param>
        /// <param name="pattern">The pattern to test against the incoming request</param>
        /// <returns>True if the request does not match the given pattern, otherwise
        /// false</returns>
        public static bool operator !=(HttpRequestSignature request, HttpRequestSignature pattern)
        {
            return !(request == pattern);
        }
    }
}
