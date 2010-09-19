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
        private class HomogeneousComparer : System.Collections.IComparer
        {
            public HomogeneousComparer() { }
            public int Compare(object lhs, object rhs) { return LindenApi.Compare(lhs, rhs, 1); }
        }

        [ScriptMethod]
        public object[] llList2List(IScriptInstance script, object[] list, int start, int end)
        {
            return GetSubList(list, start, end);
        }

        [ScriptMethod]
        public object[] llDeleteSubList(IScriptInstance script, object[] list, int start, int end)
        {
            object[] ret;

            if (start < 0)
                start = list.Length + start;

            if (start < 0)
                start = 0;

            if (end < 0)
                end = list.Length + end;
            if (end < 0)
                end = 0;

            if (start > end)
            {
                if (end >= list.Length)
                    return new object[0];

                if (start >= list.Length)
                    start = list.Length - 1;

                return GetSubList(list, end, start);
            }

            // start >= 0 && end >= 0 here
            if (start >= list.Length)
            {
                ret = new Object[list.Length];
                Array.Copy(list, 0, ret, 0, list.Length);

                return ret;
            }

            if (end >= list.Length)
                end = list.Length - 1;

            // now, this makes the math easier
            int remove = end + 1 - start;

            ret = new Object[list.Length - remove];
            if (ret.Length == 0)
                return ret;

            int src;
            int dest = 0;

            for (src = 0; src < list.Length; src++)
            {
                if (src < start || src > end)
                    ret[dest++] = list[src];
            }

            return ret;
        }

        [ScriptMethod]
        public int llListFindList(IScriptInstance script, object[] src, object[] test)
        {
            int index = -1;
            int length = src.Length - test.Length + 1;

            // If either list is empty, do not match
            if (src.Length != 0 && test.Length != 0)
            {
                for (int i = 0; i < length; i++)
                {
                    if (src[i].Equals(test[0]))
                    {
                        int j;
                        for (j = 1; j < test.Length; j++)
                        {
                            if (!src[i + j].Equals(test[j]))
                                break;
                        }

                        if (j == test.Length)
                        {
                            index = i;
                            break;
                        }
                    }
                }
            }

            return index;
        }

        [ScriptMethod]
        public object[] llListReplaceList(IScriptInstance script, object[] dest, object[] src, int start, int end)
        {
            List<object> pref;

            // Note that although we have normalized, both
            // indices could still be negative.
            if (start < 0)
            {
                start = start + dest.Length;
            }

            if (end < 0)
            {
                end = end + dest.Length;
            }
            // The comventional case, remove a sequence starting with
            // start and ending with end. And then insert the source
            // list.
            if (start <= end)
            {
                // If greater than zero, then there is going to be a
                // surviving prefix. Otherwise the inclusive nature
                // of the indices mean that we're going to add the
                // source list as a prefix.
                if (start > 0)
                {
                    pref = new List<object>(GetSubList(dest, 0, start - 1));
                    // Only add a suffix if there is something
                    // beyond the end index (it's inclusive too).
                    if (end + 1 < dest.Length)
                    {
                        pref.AddRange(src);
                        pref.AddRange(GetSubList(dest, end + 1, -1));
                        return pref.ToArray();
                    }
                    else
                    {
                        pref.AddRange(src);
                        return pref.ToArray();
                    }
                }
                // If start is less than or equal to zero, then
                // the new list is simply a prefix. We still need to
                // figure out any necessary surgery to the destination
                // based upon end. Note that if end exceeds the upper
                // bound in this case, the entire destination list
                // is removed.
                else
                {
                    if (end + 1 < dest.Length)
                    {
                        pref = new List<object>(src);
                        pref.AddRange(GetSubList(dest, end + 1, -1));
                        return pref.ToArray();
                    }
                    else
                    {
                        return src;
                    }
                }
            }
            // Finally, if start > end, we strip away a prefix and
            // a suffix, to leave the list that sits <between> ens
            // and start, and then tag on the src list. AT least
            // that's my interpretation. We can get sublist to do
            // this for us. Note that one, or both of the indices
            // might have been negative.
            else
            {
                pref = new List<object>(GetSubList(dest, end + 1, start - 1));
                pref.AddRange(src);
                return pref.ToArray();
            }
        }

        [ScriptMethod]
        public object[] llCSV2List(IScriptInstance script, string csv)
        {
            List<object> result = new List<object>();
            int parens = 0;
            int start = 0;
            int length = 0;

            for (int i = 0; i < csv.Length; i++)
            {
                switch (csv[i])
                {
                    case '<':
                        parens++;
                        length++;
                        break;
                    case '>':
                        if (parens > 0)
                            parens--;
                        length++;
                        break;
                    case ',':
                        if (parens == 0)
                        {
                            result.Add(csv.Substring(start, length).Trim());
                            start += length + 1;
                            length = 0;
                        }
                        else
                        {
                            length++;
                        }
                        break;
                    default:
                        length++;
                        break;
                }
            }

            result.Add(csv.Substring(start, length).Trim());
            return result.ToArray();
        }

        [ScriptMethod]
        public string llDumpList2String(IScriptInstance script, object[] list, string separator)
        {
            StringBuilder output = new StringBuilder();

            if (list.Length > 0)
            {
                output.Append(LSLUtils.ObjectToString(list[0]));
                for (int i = 1; i < list.Length; i++)
                    output.Append(separator + LSLUtils.ObjectToString(list[i]));
            }

            return output.ToString();
        }

        [ScriptMethod]
        public int llGetListLength(IScriptInstance script, object[] src)
        {
            return src.Length;
        }

        [ScriptMethod]
        public string llList2CSV(IScriptInstance script, object[] list)
        {
            StringBuilder output = new StringBuilder();

            if (list.Length > 0)
            {
                output.Append(LSLUtils.ObjectToString(list[0]));
                for (int i = 1; i < list.Length; i++)
                    output.Append(", " + LSLUtils.ObjectToString(list[i]));
            }

            return output.ToString();
        }

        [ScriptMethod]
        public float llList2Float(IScriptInstance script, object[] list, int index)
        {
            if (index < 0)
                index = list.Length + index;
            if (index >= list.Length || index < 0)
                return 0f;
            object o = list[index];

            if (o is Int32)
            {
                return (float)(int)o;
            }
            else if (o is Single)
            {
                return (float)o;
            }
            else if (o is String)
            {
                float f;
                LSLUtils.TryParseFloat((string)o, out f);
                return f;
            }

            return 0f;
        }

        [ScriptMethod]
        public int llList2Integer(IScriptInstance script, object[] list, int index)
        {
            if (index < 0)
                index = list.Length + index;
            if (index >= list.Length || index < 0)
                return 0;
            object o = list[index];

            if (o is Int32)
            {
                return (int)o;
            }
            else if (o is Single)
            {
                return (int)(float)o;
            }
            else if (o is String)
            {
                int i;
                LSLUtils.TryParseInt((string)o, out i);
                return i;
            }

            return 0;
        }

        [ScriptMethod]
        public string llList2Key(IScriptInstance script, object[] list, int index)
        {
            return llList2String(script, list, index);
        }

        [ScriptMethod]
        public string llList2String(IScriptInstance script, object[] list, int index)
        {
            if (index < 0)
                index = list.Length + index;
            if (index >= list.Length || index < 0)
                return String.Empty;
            object o = list[index];

            return LSLUtils.ObjectToString(o);
        }

        [ScriptMethod]
        public Vector3 llList2Vector(IScriptInstance script, object[] list, int index)
        {
            if (index < 0)
                index = list.Length + index;
            if (index >= list.Length || index < 0)
                return Vector3.Zero;
            object o = list[index];

            if (o is Vector3)
            {
                return (Vector3)o;
            }
            else if (o is String)
            {
                Vector3 v;
                LSLUtils.TryParseVector3((string)o, out v);
                return v;
            }

            return Vector3.Zero;
        }

        [ScriptMethod]
        public Quaternion llList2Rot(IScriptInstance script, object[] list, int index)
        {
            if (index < 0)
                index = list.Length + index;
            if (index >= list.Length || index < 0)
                return Quaternion.Identity;
            object o = list[index];

            if (o is Quaternion)
            {
                return (Quaternion)o;
            }
            else if (o is String)
            {
                Quaternion q;
                LSLUtils.TryParseQuaternion((string)o, out q);
                return q;
            }

            return Quaternion.Identity;
        }

        [ScriptMethod]
        public object[] llListRandomize(IScriptInstance script, object[] src)
        {
            List<object> source = new List<object>(src);
            object[] objects = new object[src.Length];

            for (int i = 0; source.Count > 0; i++)
            {
                int randomIndex = new Random().Next(source.Count);
                objects[i] = source[randomIndex];
                source.RemoveAt(randomIndex);
            }

            return objects;
        }

        [ScriptMethod]
        public object[] llListSort(IScriptInstance script, object[] list, int stride, int ascending)
        {
            if (list.Length == 0)
                return list;
            if (stride <= 0)
                stride = 1;

            object[] ret = new object[list.Length];
            Array.Copy(list, 0, ret, 0, list.Length);

            // We can optimize here in the case where stride == 1 and the list
            // consists of homogeneous types
            if (stride == 1)
            {
                bool homogeneous = true;
                int index;
                for (index = 1; index < list.Length; index++)
                {
                    if (!list[0].GetType().Equals(list[index].GetType()))
                    {
                        homogeneous = false;
                        break;
                    }
                }

                if (homogeneous)
                {
                    Array.Sort(ret, new HomogeneousComparer());
                    if (ascending == 0)
                        Array.Reverse(ret);
                    return ret;
                }
            }

            // Because of the desired type specific feathered sorting behavior
            // requried by the spec, we MUST use a non-optimized bubble sort here.
            // Anything else will give you the incorrect behavior.

            // Begin bubble sort...
            int i;
            int j;
            int k;
            int n = list.Length;

            for (i = 0; i < (n - stride); i += stride)
            {
                for (j = i + stride; j < n; j += stride)
                {
                    if (Compare(ret[i], ret[j], ascending) > 0)
                    {
                        for (k = 0; k < stride; k++)
                        {
                            object tmp = ret[i + k];
                            ret[i + k] = ret[j + k];
                            ret[j + k] = tmp;
                        }
                    }
                }
            }

            // End bubble sort
            return ret;
        }

        //[ScriptMethod]
        //public float llListStatistics(IScriptInstance script, int operation, object[] src)
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
        public object[] llParseString2List(IScriptInstance script, string str, object[] separators, object[] spacers)
        {
            return ParseString(str, separators, spacers, false);
        }

        [ScriptMethod]
        public object[] llParseStringKeepNulls(IScriptInstance script, string str, object[] separators, object[] spacers)
        {
            return ParseString(str, separators, spacers, true);
        }

        private static object[] GetSubList(object[] list, int start, int end)
        {
            object[] ret;

            // Take care of neg start or end's
            // NOTE that either index may still be negative after
            // adding the length, so we must take additional
            // measures to protect against this. Note also that
            // after normalisation the negative indices are no
            // longer relative to the end of the list.
            if (start < 0)
                start = list.Length + start;
            if (end < 0)
                end = list.Length + end;

            // The conventional case is start <= end
            // NOTE that the case of an empty list is
            // dealt with by the initial test. Start
            // less than end is taken to be the most
            // common case.

            if (start <= end)
            {
                // Start sublist beyond length
                // Also deals with start AND end still negative
                if (start >= list.Length || end < 0)
                    return new object[0];

                // Sublist extends beyond the end of the supplied list
                if (end >= list.Length)
                    end = list.Length - 1;

                // Sublist still starts before the beginning of the list
                if (start < 0)
                    start = 0;

                ret = new object[end - start + 1];
                Array.Copy(list, start, ret, 0, end - start + 1);
                return ret;
            }
            else
            {
                // Deal with the segmented case: 0->end + start->EOL
                List<object> result;

                // If end is negative, then prefix list is empty
                if (end < 0)
                {
                    result = new List<object>();
                    // If start is still negative, then the whole of
                    // the existing list is returned. This case is
                    // only admitted if end is also still negative.
                    if (start < 0)
                        return list;
                }
                else
                {
                    result = new List<object>(GetSubList(list, 0, end));
                }

                // If start is outside of list, then just return
                // the prefix, whatever it is.
                if (start >= list.Length)
                    return result.ToArray();

                result.AddRange(GetSubList(list, start, list.Length));
                return result.ToArray();
            }
        }

        private static object[] ParseString(string src, object[] separators, object[] spacers, bool keepNulls)
        {
            int beginning = 0;
            int srclen = src.Length;
            int seplen = separators.Length;
            int spclen = spacers.Length;
            int mlen = seplen + spclen;

            int[] offset = new int[mlen + 1];
            bool[] active = new bool[mlen];

            int best;
            int j;

            List<object> tokens = new List<object>();

            //    All entries are initially valid
            for (int i = 0; i < mlen; i++)
                active[i] = true;

            offset[mlen] = srclen;

            while (beginning < srclen)
            {
                best = mlen; // as bad as it gets

                // Scan for separators
                for (j = 0; j < seplen; j++)
                {
                    if (active[j])
                    {
                        // scan all of the markers
                        if ((offset[j] = src.IndexOf(separators[j].ToString(), beginning)) == -1)
                        {
                            // not present at all
                            active[j] = false;
                        }
                        else
                        {
                            // present and correct
                            if (offset[j] < offset[best])
                            {
                                // closest so far
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
                            // scan all of the markers
                            if ((offset[j] = src.IndexOf(spacers[j - seplen].ToString(), beginning)) == -1)
                            {
                                // not present at all
                                active[j] = false;
                            }
                            else
                            {
                                // present and correct
                                if (offset[j] < offset[best])
                                {
                                    // closest so far
                                    best = j;
                                }
                            }
                        }
                    }
                }

                // This is the normal exit from the scanning loop
                if (best == mlen)
                {
                    // no markers were found on this pass
                    // so we're pretty much done
                    if ((keepNulls) || ((!keepNulls) && (srclen - beginning) > 0))
                        tokens.Add(src.Substring(beginning, srclen - beginning));
                    break;
                }

                // Otherwise we just add the newly delimited token
                // and recalculate where the search should continue.
                if ((keepNulls) || ((!keepNulls) && (offset[best] - beginning) > 0))
                    tokens.Add(src.Substring(beginning, offset[best] - beginning));

                if (best < seplen)
                {
                    beginning = offset[best] + (separators[best].ToString()).Length;
                }
                else
                {
                    beginning = offset[best] + (spacers[best - seplen].ToString()).Length;
                    string str = spacers[best - seplen].ToString();
                    if ((keepNulls) || ((!keepNulls) && (str.Length > 0)))
                        tokens.Add(str);
                }
            }

            // This an awkward an not very intuitive boundary case. If the
            // last substring is a tokenizer, then there is an implied trailing
            // null list entry. Hopefully the single comparison will not be too
            // arduous. Alternatively the 'break' could be replced with a return
            // but that's shabby programming.
            if ((beginning == srclen) && (keepNulls))
            {
                if (srclen != 0)
                    tokens.Add(String.Empty);
            }

            return tokens.ToArray();
        }

        private static int Compare(object left, object right, int ascending)
        {
            // Unequal types are always "equal" for comparison purposes.
            // This way, the bubble sort will never swap them, and we'll
            // get that feathered effect we're looking for
            if (!left.GetType().Equals(right.GetType()))
                return 0;

            int ret = 0;

            if (left is Int32)
            {
                int l = (int)left;
                int r = (int)right;
                ret = Math.Sign(l - r);
            }
            else if (left is Single)
            {
                float l = (float)left;
                float r = (float)right;
                ret = Math.Sign(l - r);
            }
            else if (left is String)
            {
                string l = (string)left;
                string r = (string)right;
                ret = String.CompareOrdinal(l, r);
            }
            else if (left is Vector3)
            {
                Vector3 l = (Vector3)left;
                Vector3 r = (Vector3)right;
                ret = Math.Sign(Vector3.Mag(l) - Vector3.Mag(r));
            }
            else if (left is Quaternion)
            {
                Quaternion l = (Quaternion)left;
                Quaternion r = (Quaternion)right;
                ret = Math.Sign(QuaternionMag(l) - QuaternionMag(r));
            }

            if (ascending == 0)
                ret = -ret;

            return ret;
        }

        private static float QuaternionMag(Quaternion q)
        {
            return q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
        }
    }
}
