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
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI.HtmlControls;
using System.Xml;
using log4net;

namespace Simian
{
    public static class XrdHelper
    {
        private const string XRD_AND_HTML_TYPES = "text/html,application/xhtml+xml,application/xrd+xml,application/xml,text/xml";
        private const string XRD_TYPES = "application/xrd+xml,application/xml,text/xml";

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        public static XrdDocument FetchXRD(Uri location)
        {
            HttpWebResponse response;
            Uri xrdUrl = null;
            MemoryStream xrdStream = null;

            try
            {
                using (MemoryStream stream = FetchWebDocument(location, XRD_AND_HTML_TYPES, out response))
                {
                    if (stream != null)
                    {
                        if (IsXrdDocument(response.ContentType.ToLowerInvariant(), stream))
                        {
                            // We fetched an XRD document directly, skip ahead
                            xrdUrl = location;
                            xrdStream = stream;

                            response.Close();
                        }
                        else
                        {
                            #region LRDD

                            // 1. Check the HTTP headers for Link: <...>; rel="describedby"; ...
                            xrdUrl = FindXrdDocumentLocationInHeaders(response.Headers);

                            // 2. Check the document body for <link rel="describedby" ...>
                            if (xrdUrl == null)
                                xrdUrl = FindXrdDocumentLocationInHtmlMetaTags(stream.GetStreamString());

                            // 3. TODO: Try and grab the /host-meta document
                            if (xrdUrl == null)
                                xrdUrl = FindXrdDocumentLocationFromHostMeta(new Uri(location, "/host-meta"));

                            response.Close();

                            // 4. Fetch the XRD document
                            if (xrdUrl != null)
                            {
                                xrdStream = FetchWebDocument(xrdUrl, XRD_TYPES, out response);

                                if (!IsXrdDocument(response.ContentType.ToLowerInvariant(), xrdStream))
                                {
                                    m_log.Error("XRD fetch from " + xrdUrl + " failed");
                                    xrdStream = null;
                                }

                                response.Close();
                            }

                            #endregion LRDD
                        }

                        if (xrdStream != null)
                        {
                            XrdParser parser = new XrdParser(xrdStream);
                            XrdDocument doc = parser.Document;
                            xrdStream.Dispose();
                            return doc;
                        }
                    }
                    else
                    {
                        m_log.Warn("XRD discovery on endpoint " + location + " failed");
                    }
                }
            }
            catch (XrdParseException ex)
            {
                m_log.Warn("Failed to parse XRD document at " + location + ": " + ex.Message);
            }

            return null;
        }

        public static bool IsXrdDocument(string contentType, Stream documentStream)
        {
            if (String.IsNullOrEmpty(contentType) || documentStream == null)
                return false;

            if (contentType == "application/xrd+xml")
                return true;

            if (contentType.EndsWith("xml"))
            {
                documentStream.Seek(0, SeekOrigin.Begin);
                XmlReader reader = XmlReader.Create(documentStream);
                while (reader.Read() && reader.NodeType != XmlNodeType.Element)
                {
                    // Skip over non-element nodes
                }

                return reader.Name == "XRD";
            }

            return false;
        }

        public static string GetHighestPriorityUri(XrdDocument document, Uri relationType)
        {
            foreach (XrdLink link in document.Links)
            {
                if (link.Relation.Equals(relationType))
                    return link.Href;
            }

            return null;
        }

        private static MemoryStream FetchWebDocument(Uri location, string acceptTypes, out HttpWebResponse response)
        {
            const int MAXIMUM_BYTES = 1024 * 1024;
            const int TIMEOUT = 10000;
            const int READ_WRITE_TIMEOUT = 1500;
            const int MAXIMUM_REDIRECTS = 10;

            try
            {
                HttpWebRequest request = UntrustedHttpWebRequest.Create(location, true, READ_WRITE_TIMEOUT, TIMEOUT, MAXIMUM_REDIRECTS);
                request.Accept = acceptTypes;

                response = (HttpWebResponse)request.GetResponse();
                MemoryStream documentStream;

                using (Stream networkStream = response.GetResponseStream())
                {
                    documentStream = new MemoryStream(response.ContentLength < 0 ? 4096 : Math.Min((int)response.ContentLength, MAXIMUM_BYTES));
                    networkStream.CopyTo(documentStream, MAXIMUM_BYTES);
                    documentStream.Seek(0, SeekOrigin.Begin);
                }

                if (response.StatusCode == HttpStatusCode.OK)
                    return documentStream;
                else
                    m_log.ErrorFormat("HTTP status code {0} returned while fetching {1}", response.StatusCode, location);
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("HTTP error while fetching {0}: {1}", location, ex.Message);
            }

            response = null;
            return null;
        }

        private static Uri FindXrdDocumentLocationInHeaders(WebHeaderCollection headers)
        {
            Uri xrdUrl = null;

            string[] links = headers.GetValues("link");
            if (links != null && links.Length > 0)
            {
                for (int i = 0; i < links.Length; i++)
                {
                    string link = links[i];
                    if (link.Contains("rel=\"describedby\""))
                    {
                        if (Uri.TryCreate(Regex.Replace(link, @"^.*<(.*?)>.*$", "$1"), UriKind.Absolute, out xrdUrl))
                            break;
                    }
                }
            }

            return xrdUrl;
        }

        private static Uri FindXrdDocumentLocationInHtmlMetaTags(string html)
        {
            foreach (HtmlLink linkTag in HtmlHeadParser.HeadTags<HtmlLink>(html))
            {
                string rel = linkTag.Attributes["rel"];
                if (rel != null && rel.Equals("describedby", StringComparison.OrdinalIgnoreCase))
                {
                    Uri uri;
                    if (Uri.TryCreate(linkTag.Href, UriKind.Absolute, out uri))
                        return uri;
                }
            }

            return null;
        }

        private static Uri FindXrdDocumentLocationFromHostMeta(Uri hostMetaLocation)
        {
            // TODO: Implement this
            return null;
        }
    }
}
