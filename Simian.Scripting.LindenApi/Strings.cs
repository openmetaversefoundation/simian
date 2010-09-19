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
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace Simian.Scripting.Linden
{
    public partial class LindenApi : ISceneModule, IScriptApi
    {
        [ScriptMethod]
        public int llStringLength(IScriptInstance script, string str)
        {
            return str.Length;
        }

        [ScriptMethod]
        public string llGetSubString(IScriptInstance script, string src, int start, int end)
        {
            if (start < 0)
                start = src.Length + start;

            if (end < 0)
                end = src.Length + end;

            // Conventional substring
            if (start <= end)
            {
                // Implies both bounds are out-of-range.
                if (end < 0 || start >= src.Length)
                    return String.Empty;

                // If end is positive, then it directly corresponds to the length of the substring needed (plus one of course).
                if (end >= src.Length)
                    end = src.Length - 1;

                if (start < 0)
                    return src.Substring(0, end + 1);

                // Both indices are positive
                return src.Substring(start, (end + 1) - start);
            }

            // Inverted substring (end < start)
            else
            {
                // Implies both indices are below the lower bound.
                // In the inverted case, that means the entire string will be returned unchanged.
                if (start < 0)
                    return src;

                // If both indices are greater than the upper bound the result may seem initially counter intuitive.
                if (end >= src.Length)
                    return src;

                if (end < 0)
                {
                    if (start < src.Length)
                        return src.Substring(start);

                    return String.Empty;
                }
                else
                {
                    if (start < src.Length)
                        return src.Substring(0, end + 1) + src.Substring(start);

                    return src.Substring(0, end + 1);
                }
            }
        }

        [ScriptMethod]
        public string llDeleteSubString(IScriptInstance script, string src, int start, int end)
        {
            if (start < 0)
                start = src.Length + start;

            if (end < 0)
                end = src.Length + end;

            if (start <= end)
            {
                if (end < 0 || start >= src.Length)
                    return src;

                if (start < 0)
                    start = 0;

                if (end >= src.Length)
                    end = src.Length - 1;

                return src.Remove(start, end - start + 1);
            }
            else
            {
                if (start < 0 || end >= src.Length)
                    return String.Empty;

                if (end > 0)
                {
                    if (start < src.Length)
                        return src.Remove(start).Remove(0, end + 1);
                    else
                        return src.Remove(0, end + 1);
                }
                else
                {
                    if (start < src.Length)
                        return src.Remove(start);
                    else
                        return src;
                }
            }
        }

        [ScriptMethod]
        public string llInsertString(IScriptInstance script, string dest, int index, string src)
        {
            if (index < 0)
            {
                index = dest.Length + index;

                if (index < 0)
                    return src + dest;
            }

            if (index >= dest.Length)
                return dest + src;

            return dest.Substring(0, index) + src + dest.Substring(index);
        }

        [ScriptMethod]
        public string llStringTrim(IScriptInstance script, string src, int type)
        {
            if (type == LSLConstants.STRING_TRIM_HEAD)
                return src.TrimStart();
            else if (type == LSLConstants.STRING_TRIM_TAIL)
                return src.TrimEnd();
            else if (type == LSLConstants.STRING_TRIM)
                return src.Trim();
            else
                return src;
        }

        [ScriptMethod]
        public int llSubStringIndex(IScriptInstance script, string source, string pattern)
        {
            return source.IndexOf(pattern);
        }

        [ScriptMethod]
        public string llStringToBase64(IScriptInstance script, string str)
        {
            try { return Convert.ToBase64String(Encoding.UTF8.GetBytes(str)); }
            catch (Exception e) { throw new Exception("Error in base64Encode" + e.Message); }
        }

        [ScriptMethod]
        public string llBase64ToString(IScriptInstance script, string str)
        {
            try
            {
                Decoder utf8Decode = UTF8Encoding.UTF8.GetDecoder();

                byte[] data = Convert.FromBase64String(str);
                int charCount = utf8Decode.GetCharCount(data, 0, data.Length);
                char[] chars = new char[charCount];

                utf8Decode.GetChars(data, 0, data.Length, chars, 0);
                return new String(chars);
            }
            catch (Exception e)
            {
                throw new Exception("Error in base64Decode" + e.Message);
            }
        }

        [ScriptMethod]
        public string llXorBase64StringsCorrect(IScriptInstance script, string str1, string str2)
        {
            string ret = String.Empty;
            string src1 = llBase64ToString(script, str1);
            string src2 = llBase64ToString(script, str2);
            int c = 0;

            for (int i = 0; i < src1.Length; i++)
            {
                ret += (char)(src1[i] ^ src2[c]);

                if (++c >= src2.Length)
                    c = 0;
            }

            return llStringToBase64(script, ret);
        }

        [ScriptMethod]
        public string llSHA1String(IScriptInstance script, string src)
        {
            return Utils.SHA1String(src);
        }

        [ScriptMethod]
        public string llMD5String(IScriptInstance script, string src, string nonce)
        {
            return Utils.MD5String(src + ":" + nonce);
        }

        [ScriptMethod]
        public string llEscapeURL(IScriptInstance script, string url)
        {
            try
            {
                return Uri.EscapeDataString(url);
            }
            catch (Exception ex)
            {
                return "llEscapeURL: " + ex.Message;
            }
        }

        [ScriptMethod]
        public string llUnescapeURL(IScriptInstance script, string url)
        {
            try
            {
                return Uri.UnescapeDataString(url);
            }
            catch (Exception ex)
            {
                return "llEscapeURL: " + ex.Message;
            }
        }
    }
}