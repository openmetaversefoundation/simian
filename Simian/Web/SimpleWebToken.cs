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
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Simian
{
    public static class SimpleWebToken
    {
        public static string GenerateHmacSha256(NameValueCollection swt, byte[] sharedSecret)
        {
            return GenerateHmacSha256(WebUtil.BuildQueryString(swt), sharedSecret);
        }

        public static string GenerateHmacSha256(string swt, byte[] sharedSecret)
        {
            if (String.IsNullOrEmpty(swt))
                throw new ArgumentException("Simple Web Token cannot be null or empty", "swt");
            if (sharedSecret == null)
                throw new ArgumentException("Shared secret cannot be null", "sharedSecret");

            // Compute the hash of the swt and hmac key
            HMACSHA256 hmac = new HMACSHA256(sharedSecret);
            byte[] hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(swt));

            // Convert the hash to Base64
            return Convert.ToBase64String(hash);
        }

        public static bool VerifyHmacSha256(string swt, byte[] sharedSecret)
        {
            if (String.IsNullOrEmpty(swt) || sharedSecret == null)
                return false;

            int index = swt.LastIndexOf("&HMACSHA256=");

            if (index > 0)
            {
                // Split the SWT
                string noHMACSWT = swt.Substring(0, index);
                string submittedHMAC = swt.Substring(index + 12);

                // URL decode submittedHMAC
                submittedHMAC = HttpUtility.UrlDecode(submittedHMAC);

                // Calculate localHMAC using noHMACSWT and the HMAC key value
                string localHMAC = GenerateHmacSha256(noHMACSWT, sharedSecret);

                // Compare submittedHMAC and localHMAC to see if they are the same string
                if (submittedHMAC.Equals(localHMAC))
                {
                    // Check if the token has expired
                    return !IsExpired(swt);
                }
            }

            return false;
        }

        public static bool IsExpired(string swt)
        {
            if (String.IsNullOrEmpty(swt))
                return true;

            int index = swt.LastIndexOf("&ExpiresOn=");

            if (index > 0)
            {
                // Split the SWT
                swt = swt.Substring(index + 11);
                index = swt.IndexOf('&');

                if (index > 0)
                {
                    // Remove everything after the expiration timestamp
                    swt = swt.Substring(0, index);

                    int timestamp;
                    if (Int32.TryParse(swt, out timestamp))
                    {
                        // Convert the timestamp and compare against the current (UTC) time
                        DateTime expirationDate = OpenMetaverse.Utils.UnixTimeToDateTime(timestamp);
                        return DateTime.UtcNow > expirationDate;
                    }
                }
            }

            return false;
        }
    }
}
