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
using System.Text;
using System.Xml;
using System.Xml.XPath;

namespace Simian
{
    public class XrdParseException : Exception
    {
        public XrdParseException() : base() { }
        public XrdParseException(string msg) : base(msg) { }
        public XrdParseException(string msg, System.Exception inner) : base(msg, inner) { }
    }

    public class XrdLink
    {
        public readonly Uri Relation;
        public readonly string Type;
        public readonly string Href;
        public readonly string Template;

        public XrdLink(Uri relation, string type, string href, string template)
        {
            Relation = relation;
            Type = type;
            Href = href;
            Template = template;
        }
    }

    public class XrdDocument
    {
        public readonly Uri Subject;
        public readonly DateTime? Expires;
        public readonly List<Uri> Aliases;
        public readonly List<XrdLink> Links;

        public XrdDocument(Uri subject)
        {
            Subject = subject;
            Aliases = new List<Uri>();
            Links = new List<XrdLink>();
        }

        public XrdDocument(Uri subject, DateTime? expires)
        {
            Subject = subject;
            Expires = expires;
            Aliases = new List<Uri>();
            Links = new List<XrdLink>();
        }

        public XrdDocument(Uri subject, DateTime? expires, List<Uri> aliases, List<XrdLink> links)
        {
            Subject = subject;
            Expires = expires;
            Aliases = aliases;
            Links = links;
        }
    }

    public class XrdParser
    {
        private XPathDocument doc;
        private XPathNavigator cursor;
        private XrdDocument result;
        private bool parsed = false;

        public XrdParser(Stream xrd)
        {
            doc = new XPathDocument(xrd);
            result = new XrdDocument(null);
            cursor = doc.CreateNavigator();
        }

        public XrdDocument Document
        {
            get
            {
                if (!parsed) Parse();
                return result;
            }
        }

        private void Parse()
        {
            XmlNamespaceManager nsMgr = new XmlNamespaceManager(cursor.NameTable);
            nsMgr.AddNamespace("xrd", "http://docs.oasis-open.org/ns/xri/xrd-1.0");

            var expires = cursor.SelectSingleNode("/xrd:XRD/xrd:Expires", nsMgr);
            var subject = cursor.SelectSingleNode("/xrd:XRD/xrd:Subject", nsMgr);
            var aliases = GetAll(cursor.Select("/xrd:XRD/xrd:Alias", nsMgr));

            if (subject == null)
                throw new XrdParseException("Missing Subject");

            Uri subjectUri;
            if (Uri.TryCreate(subject.Value, UriKind.Absolute, out subjectUri))
            {
                DateTime? expirationDate = null;
                if (expires != null)
                    expirationDate = expires.ValueAsDateTime;

                List<XrdLink> links = new List<XrdLink>();

                XPathNodeIterator linkIter = cursor.Select("/xrd:XRD/xrd:Link", nsMgr);
                while (linkIter.MoveNext())
                {
                    var rel = linkIter.Current.SelectSingleNode("@rel", nsMgr);
                    var type = linkIter.Current.SelectSingleNode("@type", nsMgr);
                    var href = linkIter.Current.SelectSingleNode("@href", nsMgr);
                    var template = linkIter.Current.SelectSingleNode("@template", nsMgr);

                    Uri relUrl;
                    if (rel != null && Uri.TryCreate(rel.Value, UriKind.Absolute, out relUrl))
                    {
                        string typeStr = null;
                        if (type != null)
                            typeStr = type.Value;

                        string hrefUrl = null;
                        if (href != null)
                            hrefUrl = href.Value;

                        string templateStr = null;
                        if (template != null)
                            templateStr = template.Value;

                        XrdLink link = new XrdLink(relUrl, typeStr, hrefUrl, templateStr);
                        links.Add(link);
                    }
                }

                // Only keep the aliases that can be parsed as valid absolute URIs
                List<Uri> validAliases = new List<Uri>(aliases.Count);
                foreach (string alias in aliases)
                {
                    Uri aliasUri;
                    if (Uri.TryCreate(alias, UriKind.Absolute, out aliasUri))
                        validAliases.Add(aliasUri);
                }

                result = new XrdDocument(subjectUri, expirationDate, validAliases, links);
            }
        }

        private List<string> GetAll(XPathNodeIterator iter)
        {
            var list = new List<string>(iter.Count);
            while (iter.MoveNext())
                list.Add(iter.Current.Value);
            return list;
        }
    }
}
