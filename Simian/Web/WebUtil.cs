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
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using log4net;
using HttpServer;
using OpenMetaverse.StructuredData;

namespace Simian
{
    /// <summary>
    /// Miscellaneous static methods and extension methods related to the web
    /// </summary>
    public static class WebUtil
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        /// <summary>
        /// Send LLSD to an HTTP client in application/llsd+json form
        /// </summary>
        /// <param name="response">HTTP response to send the data in</param>
        /// <param name="body">LLSD to send to the client</param>
        public static void SendJSONResponse(IHttpResponse response, OSDMap body)
        {
            byte[] responseData = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(body));

            response.Encoding = Encoding.UTF8;
            response.ContentLength = responseData.Length;
            response.ContentType = "application/llsd+json";
            response.Body.Write(responseData, 0, responseData.Length);
        }

        /// <summary>
        /// Send LLSD to an HTTP client in application/llsd+xml form
        /// </summary>
        /// <param name="response">HTTP response to send the data in</param>
        /// <param name="body">LLSD to send to the client</param>
        public static void SendXMLResponse(IHttpResponse response, OSDMap body)
        {
            byte[] responseData = OSDParser.SerializeLLSDXmlBytes(body);

            response.Encoding = Encoding.UTF8;
            response.ContentLength = responseData.Length;
            response.ContentType = "application/llsd+xml";
            response.Body.Write(responseData, 0, responseData.Length);
        }

        /// <summary>
        /// Fetches a URL and returns the response or error message as a string
        /// </summary>
        /// <param name="url">URL to fetch with GET</param>
        /// <param name="responseStr">Will contain the HTTP body data, or the 
        /// error message on failure</param>
        /// <returns>True if the request succeeded, otherwise false</returns>
        public static bool TryGetUrl(string url, out string responseStr)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);

                using (WebResponse response = request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        try
                        {
                            responseStr = responseStream.GetStreamString();
                            return true;
                        }
                        catch
                        {
                            responseStr = "Failed to parse the response.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.Warn("GET on URL " + url + " failed: " + ex.Message);
                responseStr = ex.Message;
            }

            return false;
        }

        /// <summary>
        /// Retrieves a web service using HTTP GET and returns it as an OSDMap
        /// </summary>
        /// <param name="url">URL to fetch</param>
        /// <returns>An OSDMap containing the response. If an error occurred, the map will contain 
        /// a key/value pair named Message</returns>
        public static OSDMap GetService(string url)
        {
            string errorMessage;

            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "GET";

                using (WebResponse response = request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        try
                        {
                            string responseStr = responseStream.GetStreamString();
                            OSD responseOSD = OSDParser.Deserialize(responseStr);
                            if (responseOSD.Type == OSDType.Map)
                                return (OSDMap)responseOSD;
                            else
                                errorMessage = "Response format was invalid (" + responseOSD.Type + ")";
                        }
                        catch
                        {
                            errorMessage = "Failed to parse the response (" + responseStream.Length + " bytes of " + response.ContentType + ")";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.Warn("GET from URL " + url + " failed: " + ex);
                errorMessage = ex.Message;
            }

            return new OSDMap { { "Message", OSD.FromString("Service request failed. " + errorMessage) } };
        }

        /// <summary>
        /// POST URL-encoded form data to a web service that returns LLSD or
        /// JSON data
        /// </summary>
        public static OSDMap PostToService(string url, NameValueCollection data)
        {
            string errorMessage;

            try
            {
                string queryString = BuildQueryString(data);
                byte[] requestData = System.Text.Encoding.UTF8.GetBytes(queryString);

                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "POST";
                request.ContentLength = requestData.Length;
                request.ContentType = "application/x-www-form-urlencoded";

                Stream requestStream = request.GetRequestStream();
                requestStream.Write(requestData, 0, requestData.Length);
                requestStream.Close();

                using (WebResponse response = request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        try
                        {
                            string responseStr = responseStream.GetStreamString();
                            OSD responseOSD = OSDParser.Deserialize(responseStr);
                            if (responseOSD.Type == OSDType.Map)
                                return (OSDMap)responseOSD;
                            else
                                errorMessage = "Response format was invalid (" + responseOSD.Type + ")";
                        }
                        catch
                        {
                            errorMessage = "Failed to parse the response (" + responseStream.Length + " bytes of " + response.ContentType + ")";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.Warn("POST to URL " + url + " failed: " + ex);
                errorMessage = ex.Message;
            }

            return new OSDMap { { "Message", OSD.FromString("Service request failed. " + errorMessage) } };
        }

        /// <summary>
        /// Encodes a url string
        /// </summary>
        /// <param name="str">String to encode</param>
        /// <returns>URL-encoded string</returns>
        /// <remarks>Spaces will be encoded as a percent symbol followed by 20,
        /// not a plus sign</remarks>
        public static string UrlEncode(string str)
        {
            str = System.Web.HttpUtility.UrlEncode(str);
            return str.Replace("+", "%20");
        }

        #region Uri

        /// <summary>
        /// Combines a Uri that can contain both a base Uri and relative path
        /// with a second relative path fragment
        /// </summary>
        /// <param name="uri">Starting (base) Uri</param>
        /// <param name="fragment">Relative path fragment to append to the end
        /// of the Uri</param>
        /// <returns>The combined Uri</returns>
        /// <remarks>This is similar to the Uri constructor that takes a base
        /// Uri and the relative path, except this method can append a relative
        /// path fragment on to an existing relative path</remarks>
        public static Uri Combine(this Uri uri, string fragment)
        {
            string fragment1 = uri.Fragment;
            string fragment2 = fragment;

            if (!fragment1.EndsWith("/"))
                fragment1 = fragment1 + '/';
            if (fragment2.StartsWith("/"))
                fragment2 = fragment2.Substring(1);

            return new Uri(uri, fragment1 + fragment2);
        }

        /// <summary>
        /// Combines a Uri that can contain both a base Uri and relative path
        /// with a second relative path fragment. If the fragment is absolute,
        /// it will be returned without modification
        /// </summary>
        /// <param name="uri">Starting (base) Uri</param>
        /// <param name="fragment">Relative path fragment to append to the end
        /// of the Uri, or an absolute Uri to return unmodified</param>
        /// <returns>The combined Uri</returns>
        public static Uri Combine(this Uri uri, Uri fragment)
        {
            if (fragment.IsAbsoluteUri)
                return fragment;

            string fragment1 = uri.Fragment;
            string fragment2 = fragment.ToString();

            if (!fragment1.EndsWith("/"))
                fragment1 = fragment1 + '/';
            if (fragment2.StartsWith("/"))
                fragment2 = fragment2.Substring(1);

            return new Uri(uri, fragment1 + fragment2);
        }

        /// <summary>
        /// Appends a query string to a Uri that may or may not have existing 
        /// query parameters
        /// </summary>
        /// <param name="uri">Uri to append the query to</param>
        /// <param name="query">Query string to append. Can either start with ?
        /// or just containg key/value pairs</param>
        /// <returns>String representation of the Uri with the query string
        /// appended</returns>
        public static string AppendQuery(this Uri uri, string query)
        {
            if (String.IsNullOrEmpty(query))
                return uri.ToString();

            if (query[0] == '?' || query[0] == '&')
                query = query.Substring(1);

            string uriStr = uri.ToString();

            if (uriStr.Contains("?"))
                return uriStr + '&' + query;
            else
                return uriStr + '?' + query;
        }

        #endregion Uri

        #region NameValueCollection

        /// <summary>
        /// Convert a NameValueCollection into a query string. This is the
        /// inverse of HttpUtility.ParseQueryString()
        /// </summary>
        /// <param name="parameters">Collection of key/value pairs to convert</param>
        /// <returns>A query string with URL-escaped values</returns>
        public static string BuildQueryString(NameValueCollection parameters)
        {
            List<string> items = new List<string>(parameters.Count);

            foreach (string key in parameters.Keys)
            {
                string[] values = parameters.GetValues(key);
                if (values != null && values.Length > 0)
                {
                    foreach (string value in values)
                        items.Add(String.Concat(key, "=", WebUtil.UrlEncode(value ?? String.Empty)));
                }
                else
                {
                    items.Add(key + "=");
                }
            }

            return String.Join("&", items.ToArray());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetOne(this NameValueCollection collection, string key)
        {
            string[] values = collection.GetValues(key);
            if (values != null && values.Length > 0)
                return values[0];

            return null;
        }

        #endregion NameValueCollection

        #region Stream

        /// <summary>
        /// Copies the contents of one stream to another, starting at the 
        /// current position of each stream
        /// </summary>
        /// <param name="copyFrom">The stream to copy from, at the position 
        /// where copying should begin</param>
        /// <param name="copyTo">The stream to copy to, at the position where 
        /// bytes should be written</param>
        /// <param name="maximumBytesToCopy">The maximum bytes to copy</param>
        /// <returns>The total number of bytes copied</returns>
        /// <remarks>
        /// Copying begins at the streams' current positions. The positions are
        /// NOT reset after copying is complete.
        /// </remarks>
        public static int CopyTo(this Stream copyFrom, Stream copyTo, int maximumBytesToCopy)
        {
            byte[] buffer = new byte[4096];
            int readBytes;
            int totalCopiedBytes = 0;

            while ((readBytes = copyFrom.Read(buffer, 0, Math.Min(4096, maximumBytesToCopy))) > 0)
            {
                int writeBytes = Math.Min(maximumBytesToCopy, readBytes);
                copyTo.Write(buffer, 0, writeBytes);
                totalCopiedBytes += writeBytes;
                maximumBytesToCopy -= writeBytes;
            }

            return totalCopiedBytes;
        }

        /// <summary>
        /// Converts an entire stream to a string, regardless of current stream
        /// position
        /// </summary>
        /// <param name="stream">The stream to convert to a string</param>
        /// <returns></returns>
        /// <remarks>When this method is done, the stream position will be 
        /// reset to its previous position before this method was called</remarks>
        public static string GetStreamString(this Stream stream)
        {
            string value = null;

            if (stream != null && stream.CanRead)
            {
                long rewindPos = -1;

                if (stream.CanSeek && stream.Position != 0)
                {
                    rewindPos = stream.Position;
                    stream.Seek(0, SeekOrigin.Begin);
                }

                StreamReader reader = new StreamReader(stream);
                value = reader.ReadToEnd();

                if (rewindPos >= 0)
                    stream.Seek(rewindPos, SeekOrigin.Begin);
            }

            return value;
        }

        #endregion Stream
    }
}
