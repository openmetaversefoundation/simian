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

namespace Simian
{
    /// <summary>
    /// Contains a signature pattern (for matching against incoming
    /// requests) and a callback for handling the request
    /// </summary>
    public sealed class HttpRequestHandler : IEquatable<HttpRequestHandler>
    {
        /// <summary>Signature pattern to match against incoming requests</summary>
        public HttpRequestSignature Signature;
        /// <summary>Callback for handling requests that match the signature</summary>
        public HttpRequestCallback Callback;
        /// <summary>If true, the IHttpResponse will be sent to the client after
        /// the callback completes. Otherwise, the connection will be left open 
        /// and the user is responsible for closing the connection later</summary>
        public bool SendResponseAfterCallback;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="signature">Signature pattern for matching against incoming requests</param>
        /// <param name="callback">Callback for handling the request</param>
        /// <param name="sendResponseAfterCallback">If true, the IHttpResponse will be sent 
        /// to the client after the callback completes. Otherwise, the connection will be left
        /// open and the user is responsible for closing the connection later</param>
        public HttpRequestHandler(HttpRequestSignature signature, HttpRequestCallback callback, bool sendResponseAfterCallback)
        {
            Signature = signature;
            Callback = callback;
            SendResponseAfterCallback = sendResponseAfterCallback;
        }

        /// <summary>
        /// Equality comparison
        /// </summary>
        /// <param name="obj">Object to compare against for equality</param>
        /// <returns>True if the given object is equal to this object, otherwise false</returns>
        public override bool Equals(object obj)
        {
            return (obj is HttpRequestHandler) ? this.Signature == ((HttpRequestHandler)obj).Signature : false;
        }

        public override string ToString()
        {
            return Signature.ToString();
        }

        /// <summary>
        /// Equality comparison
        /// </summary>
        /// <param name="handler">Object to compare against for equality</param>
        /// <returns>True if the given object is equal to this object, otherwise false</returns>
        public bool Equals(HttpRequestHandler handler)
        {
            return handler != null && this.Signature == handler.Signature;
        }

        /// <summary>
        /// Returns the hash code for the signature in this handler
        /// </summary>
        /// <returns>The hash code for the signature in this handler</returns>
        public override int GetHashCode()
        {
            return Signature.GetHashCode();
        }

        public static bool operator ==(HttpRequestHandler left, HttpRequestHandler right)
        {
            if ((object)left == null)
                return (object)right == null;
            else if ((object)right == null)
                return false;
            else
                return left.Signature == right.Signature;
        }

        public static bool operator !=(HttpRequestHandler left, HttpRequestHandler right)
        {
            return !(left == right);
        }
    }
}
