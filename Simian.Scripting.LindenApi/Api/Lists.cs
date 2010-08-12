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
        public lsl_list llDeleteSubList(IScriptInstance script, lsl_list list, int start, int end)
        {
            return list.DeleteSublist(start, end);
        }

        [ScriptMethod]
        public lsl_list llGetSubList(IScriptInstance script, lsl_list src, int start, int end)
        {
            return src.GetSublist(start, end);
        }

        [ScriptMethod]
        public lsl_list llCSV2List(IScriptInstance script, string csv)
        {
            return lsl_list.FromCSV(csv);
        }

        [ScriptMethod]
        public string llDumpList2String(IScriptInstance script, lsl_list src, string separator)
        {
            int len = src.Length;

            string[] arr = new string[len];

            for (int i = 0; i < len; i++)
                arr[i] = src.Data[i].ToString();

            return String.Join(separator, arr);
        }

        [ScriptMethod]
        public int llGetListLength(IScriptInstance script, lsl_list src)
        {
            return src.Length;
        }

        [ScriptMethod]
        public string llList2CSV(IScriptInstance script, lsl_list list)
        {
            return list.ToCSV();
        }

        [ScriptMethod]
        public float llList2Float(IScriptInstance script, lsl_list list, int index)
        {
            float ret;
            if (Single.TryParse(list.Data[index].ToString(), out ret))
                return ret;
            else
                return 0f;
        }

        [ScriptMethod]
        public int llList2Integer(IScriptInstance script, lsl_list list, int index)
        {
            int ret;
            if (Int32.TryParse(list.Data[index].ToString(), out ret))
                return ret;
            else
                return 0;
        }

        [ScriptMethod]
        public string llList2Key(IScriptInstance script, lsl_list list, int index)
        {
            return list.Data[index].ToString();
        }

        [ScriptMethod]
        public lsl_list llList2List(IScriptInstance script, lsl_list list, int start, int end)
        {
            return list.GetSublist(start, end);
        }

        [ScriptMethod]
        public lsl_list llListRandomize(IScriptInstance script, lsl_list src)
        {
            List<object> items = new List<object>(src.Data);
            object[] objects = new object[src.Data.Length];

            for (int i=0; items.Count > 0; i++)
            {
                int randomIndex = new Random().Next(items.Count);
                objects[i] = items[randomIndex];
                items.RemoveAt(randomIndex);
            }

            return new lsl_list(objects);
        }

        [ScriptMethod]
        public string llList2String(IScriptInstance script, lsl_list src, int index)
        {
            return src.Data[index].ToString();
        }

        [ScriptMethod]
        public Vector3 llList2Vector(IScriptInstance script, lsl_list src, int index)
        {
            Vector3 ret;
            if (Vector3.TryParse(src.Data[index].ToString(), out ret))
                return ret;
            else
                return Vector3.Zero;
        }

        [ScriptMethod]
        public lsl_list llListSort(IScriptInstance script, lsl_list src, int stride, int ascending)
        {
            return src.Sort(stride, ascending);
        }

        [ScriptMethod]
        public double llListStatistics(IScriptInstance script, int operation, lsl_list src)
        {
            switch (operation)
            {
                case LSLScriptBase.LIST_STAT_GEOMETRIC_MEAN:
                    return src.GeometricMean();

                case LSLScriptBase.LIST_STAT_HARMONIC_MEAN:
                    return src.HarmonicMean();

                case LSLScriptBase.LIST_STAT_MAX:
                    return src.Max();

                case LSLScriptBase.LIST_STAT_MEAN:
                    return src.Mean();

                case LSLScriptBase.LIST_STAT_MEDIAN:
                    return src.Median();

                case LSLScriptBase.LIST_STAT_MIN:
                    return src.Min();

                case LSLScriptBase.LIST_STAT_NUM_COUNT:
                    return src.NumericLength();

                case LSLScriptBase.LIST_STAT_RANGE:
                    return src.Range();

                case LSLScriptBase.LIST_STAT_STD_DEV:
                    return src.StdDev();

                case LSLScriptBase.LIST_STAT_SUM:
                    return src.Sum();

                case LSLScriptBase.LIST_STAT_SUM_SQUARES:
                    return src.SumSqrs();

                default:
                    return 0f;
            }
        }
    }
}
