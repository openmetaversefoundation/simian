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
        //[ScriptMethod]
        //public List<object> llDeleteSubList(IScriptInstance script, List<object> list, int start, int end)
        //{
        //}

        //[ScriptMethod]
        //public List<object> llGetSubList(IScriptInstance script, List<object> src, int start, int end)
        //{
        //}

        //[ScriptMethod]
        //public List<object> llCSV2List(IScriptInstance script, string csv)
        //{
        //}

        //[ScriptMethod]
        //public string llDumpList2String(IScriptInstance script, List<object> src, string separator)
        //{
        //}

        [ScriptMethod]
        public int llGetListLength(IScriptInstance script, List<object> src)
        {
            return src.Count;
        }

        //[ScriptMethod]
        //public string llList2CSV(IScriptInstance script, List<object> list)
        //{
        //}

        //[ScriptMethod]
        //public float llList2Float(IScriptInstance script, List<object> list, int index)
        //{
        //}

        //[ScriptMethod]
        //public int llList2Integer(IScriptInstance script, List<object> list, int index)
        //{
        //}

        //[ScriptMethod]
        //public string llList2Key(IScriptInstance script, List<object> list, int index)
        //{
        //}

        //[ScriptMethod]
        //public List<object> llList2List(IScriptInstance script, List<object> list, int start, int end)
        //{
        //}

        [ScriptMethod]
        public List<object> llListRandomize(IScriptInstance script, List<object> src)
        {
            object[] objects = new object[src.Count];

            for (int i = 0; src.Count > 0; i++)
            {
                int randomIndex = new Random().Next(src.Count);
                objects[i] = src[randomIndex];
                src.RemoveAt(randomIndex);
            }

            return new List<object>(objects);
        }

        //[ScriptMethod]
        //public string llList2String(IScriptInstance script, List<object> src, int index)
        //{
        //}

        //[ScriptMethod]
        //public Vector3 llList2Vector(IScriptInstance script, List<object> src, int index)
        //{
        //}

        //[ScriptMethod]
        //public List<object> llListSort(IScriptInstance script, List<object> src, int stride, int ascending)
        //{
        //}

        //[ScriptMethod]
        //public double llListStatistics(IScriptInstance script, int operation, List<object> src)
        //{
        //    switch (operation)
        //    {
        //        case LSLConstants.LIST_STAT_GEOMETRIC_MEAN:
        //            ;
        //        case LSLConstants.LIST_STAT_HARMONIC_MEAN:
        //            ;
        //        case LSLConstants.LIST_STAT_MAX:
        //            ;
        //        case LSLConstants.LIST_STAT_MEAN:
        //            ;
        //        case LSLConstants.LIST_STAT_MEDIAN:
        //            ;
        //        case LSLConstants.LIST_STAT_MIN:
        //            ;
        //        case LSLConstants.LIST_STAT_NUM_COUNT:
        //            ;
        //        case LSLConstants.LIST_STAT_RANGE:
        //            ;
        //        case LSLConstants.LIST_STAT_STD_DEV:
        //            ;
        //        case LSLConstants.LIST_STAT_SUM:
        //            ;
        //        case LSLConstants.LIST_STAT_SUM_SQUARES:
        //            ;
        //        default:
        //            return 0f;
        //    }
        //}

        [ScriptMethod]
        public List<object> llParseString2List(IScriptInstance script, string str, List<object> separators, List<object> in_spacers)
        {
            List<object> ret = new List<object>();
            List<object> spacers = new List<object>();

            if (in_spacers.Count > 0 && separators.Count > 0)
            {
                for (int i = 0; i < in_spacers.Count; i++)
                {
                    object s = in_spacers[i];
                    for (int j = 0; j < separators.Count; j++)
                    {
                        if (separators[j].ToString() == s.ToString())
                        {
                            s = null;
                            break;
                        }
                    }

                    if (s != null)
                        spacers.Add(s);
                }
            }

            object[] delimiters = new object[separators.Count + spacers.Count];
            separators.ToArray().CopyTo(delimiters, 0);
            spacers.ToArray().CopyTo(delimiters, separators.Count);

            bool dfound;
            do
            {
                dfound = false;
                int cindex = -1;
                string cdeli = String.Empty;
                for (int i = 0; i < delimiters.Length; i++)
                {
                    int index = str.IndexOf(delimiters[i].ToString());
                    bool found = index != -1;
                    if (found && String.Empty != delimiters[i].ToString())
                    {
                        if ((cindex > index) || (cindex == -1))
                        {
                            cindex = index;
                            cdeli = delimiters[i].ToString();
                        }
                        dfound = dfound || found;
                    }
                }
                if (cindex != -1)
                {
                    if (cindex > 0)
                        ret.Add(str.Substring(0, cindex));

                    for (int j = 0; j < spacers.Count; j++)
                    {
                        if (spacers[j].ToString() == cdeli)
                        {
                            ret.Add(cdeli);
                            break;
                        }
                    }

                    str = str.Substring(cindex + cdeli.Length);
                }
            } while (dfound);

            if (!String.IsNullOrEmpty(str))
                ret.Add(str);

            return ret;
        }

        [ScriptMethod]
        public List<object> llParseStringKeepNulls(IScriptInstance script, string src, List<object> separators, List<object> spacers)
        {
            int beginning = 0;
            int srclen = src.Length;
            int seplen = separators.Count;
            object[] separray = separators.ToArray();
            int spclen = spacers.Count;
            object[] spcarray = spacers.ToArray();
            int mlen = seplen + spclen;

            int[] offset = new int[mlen + 1];
            bool[] active = new bool[mlen];

            int best;
            int j;

            List<object> tokens = new List<object>();

            // All entries are initially valid
            for (int i = 0; i < mlen; i++)
                active[i] = true;

            offset[mlen] = srclen;

            while (beginning < srclen)
            {
                best = mlen; // As bad as it gets

                // Scan for separators
                for (j = 0; j < seplen; j++)
                {
                    if (active[j])
                    {
                        // Scan all of the markers
                        if ((offset[j] = src.IndexOf(separray[j].ToString(), beginning)) == -1)
                        {
                            // Not present at all
                            active[j] = false;
                        }
                        else
                        {
                            // Present and correct
                            if (offset[j] < offset[best])
                            {
                                // Closest so far
                                best = j;
                                if (offset[best] == beginning)
                                    break;
                            }
                        }
                    }
                }

                // Scan for spacers
                if (offset[best] != beginning)
                {
                    for (j = seplen; (j < mlen) && (offset[best] > beginning); j++)
                    {
                        if (active[j])
                        {
                            // Scan all of the markers
                            if ((offset[j] = src.IndexOf(spcarray[j - seplen].ToString(), beginning)) == -1)
                            {
                                // Not present at all
                                active[j] = false;
                            }
                            else
                            {
                                // Present and correct
                                if (offset[j] < offset[best])
                                {
                                    // Closest so far
                                    best = j;
                                }
                            }
                        }
                    }
                }

                // This is the normal exit from the scanning loop
                if (best == mlen)
                {
                    // No markers were found on this pass
                    // so we're pretty much done
                    tokens.Add(src.Substring(beginning, srclen - beginning));
                    break;
                }

                // Otherwise we just add the newly delimited token
                // and recalculate where the search should continue
                tokens.Add(src.Substring(beginning, offset[best] - beginning));

                if (best < seplen)
                {
                    beginning = offset[best] + (separray[best].ToString()).Length;
                }
                else
                {
                    beginning = offset[best] + (spcarray[best - seplen].ToString()).Length;
                    tokens.Add(spcarray[best - seplen].ToString());
                }
            }

            // This an awkward an not very intuitive boundary case. If the
            // last substring is a tokenizer, then there is an implied trailing
            // null list entry. Hopefully the single comparison will not be too
            // arduous. Alternatively the 'break' could be replaced with a return
            // but that's shabby programming
            if (beginning == srclen)
            {
                if (srclen != 0)
                    tokens.Add(String.Empty);
            }

            return tokens;
        }
    }
}
