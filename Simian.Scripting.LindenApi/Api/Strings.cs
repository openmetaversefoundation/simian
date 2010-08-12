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

    }
}