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
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using OpenMetaverse;

namespace Simian.Scripting.Linden
{
    [Serializable]
    public struct lsl_vector
    {
        public double x;
        public double y;
        public double z;

        #region Constructors

        public lsl_vector(lsl_vector vector)
        {
            x = (float)vector.x;
            y = (float)vector.y;
            z = (float)vector.z;
        }

        public lsl_vector(double X, double Y, double Z)
        {
            x = X;
            y = Y;
            z = Z;
        }

        public lsl_vector(string str)
        {
            str = str.Replace('<', ' ');
            str = str.Replace('>', ' ');
            string[] tmps = str.Split(new Char[] { ',', '<', '>' });
            if (tmps.Length < 3)
            {
                x = y = z = 0;
                return;
            }
            bool res;
            res = Double.TryParse(tmps[0], out x);
            res = res & Double.TryParse(tmps[1], out y);
            res = res & Double.TryParse(tmps[2], out z);
        }

        #endregion

        #region Overriders

        public override string ToString()
        {
            return String.Format("<{0:0.000000}, {1:0.000000}, {2:0.000000}>", x, y, z);
        }

        public static explicit operator lsl_string(lsl_vector vec)
        {
            return new lsl_string(vec.ToString());
        }

        public static explicit operator string(lsl_vector vec)
        {
            return vec.ToString();
        }

        public static explicit operator lsl_vector(string s)
        {
            return new lsl_vector(s);
        }

        public static implicit operator lsl_list(lsl_vector vec)
        {
            return new lsl_list(new object[] { vec });
        }

        public static implicit operator lsl_vector(Vector3 v)
        {
            return new lsl_vector(v.X, v.Y, v.Z);
        }

        public static implicit operator Vector3(lsl_vector vector)
        {
            return new Vector3((float)vector.x, (float)vector.y, (float)vector.z);
        }

        public static bool operator ==(lsl_vector lhs, lsl_vector rhs)
        {
            return (lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z);
        }

        public static bool operator !=(lsl_vector lhs, lsl_vector rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return (x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode());
        }

        public override bool Equals(object o)
        {
            if (!(o is lsl_vector)) return false;

            lsl_vector vector = (lsl_vector)o;

            return (x == vector.x && y == vector.y && z == vector.z);
        }

        public static lsl_vector operator -(lsl_vector vector)
        {
            return new lsl_vector(-vector.x, -vector.y, -vector.z);
        }

        #endregion

        #region Vector & Vector Math

        // Vector-Vector Math
        public static lsl_vector operator +(lsl_vector lhs, lsl_vector rhs)
        {
            return new lsl_vector(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z);
        }

        public static lsl_vector operator -(lsl_vector lhs, lsl_vector rhs)
        {
            return new lsl_vector(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z);
        }

        public static lsl_float operator *(lsl_vector lhs, lsl_vector rhs)
        {
            return Dot(lhs, rhs);
        }

        public static lsl_vector operator %(lsl_vector v1, lsl_vector v2)
        {
            //Cross product
            lsl_vector tv;
            tv.x = (v1.y * v2.z) - (v1.z * v2.y);
            tv.y = (v1.z * v2.x) - (v1.x * v2.z);
            tv.z = (v1.x * v2.y) - (v1.y * v2.x);
            return tv;
        }

        #endregion

        #region Vector & Float Math

        // Vector-Float and Float-Vector Math
        public static lsl_vector operator *(lsl_vector vec, float val)
        {
            return new lsl_vector(vec.x * val, vec.y * val, vec.z * val);
        }

        public static lsl_vector operator *(float val, lsl_vector vec)
        {
            return new lsl_vector(vec.x * val, vec.y * val, vec.z * val);
        }

        public static lsl_vector operator /(lsl_vector v, float f)
        {
            v.x = v.x / f;
            v.y = v.y / f;
            v.z = v.z / f;
            return v;
        }

        #endregion

        #region Vector & Double Math

        public static lsl_vector operator *(lsl_vector vec, double val)
        {
            return new lsl_vector(vec.x * val, vec.y * val, vec.z * val);
        }

        public static lsl_vector operator *(double val, lsl_vector vec)
        {
            return new lsl_vector(vec.x * val, vec.y * val, vec.z * val);
        }

        public static lsl_vector operator /(lsl_vector v, double f)
        {
            v.x = v.x / f;
            v.y = v.y / f;
            v.z = v.z / f;
            return v;
        }

        #endregion

        #region Vector & Rotation Math

        // Vector-Rotation Math
        public static lsl_vector operator *(lsl_vector v, lsl_rotation r)
        {
            lsl_rotation vq = new lsl_rotation(v.x, v.y, v.z, 0);
            lsl_rotation nq = new lsl_rotation(-r.x, -r.y, -r.z, r.s);

            // adapted for operator * computing "b * a"
            lsl_rotation result = nq * (vq * r);

            return new lsl_vector(result.x, result.y, result.z);
        }

        public static lsl_vector operator /(lsl_vector v, lsl_rotation r)
        {
            r.s = -r.s;
            return v * r;
        }

        #endregion

        #region Static Helper Functions

        public static double Dot(lsl_vector v1, lsl_vector v2)
        {
            return (v1.x * v2.x) + (v1.y * v2.y) + (v1.z * v2.z);
        }

        public static lsl_vector Cross(lsl_vector v1, lsl_vector v2)
        {
            return new lsl_vector
                (
                v1.y * v2.z - v1.z * v2.y,
                v1.z * v2.x - v1.x * v2.z,
                v1.x * v2.y - v1.y * v2.x
                );
        }

        public static double Mag(lsl_vector v)
        {
            return Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        }

        public static lsl_vector Norm(lsl_vector vector)
        {
            double mag = Mag(vector);
            return new lsl_vector(vector.x / mag, vector.y / mag, vector.z / mag);
        }

        #endregion

        public static readonly lsl_vector Zero = new lsl_vector(0d, 0d, 0d);
    }

    [Serializable]
    public struct lsl_rotation
    {
        public double x;
        public double y;
        public double z;
        public double s;

        #region Constructors

        public lsl_rotation(lsl_rotation Quat)
        {
            x = (float)Quat.x;
            y = (float)Quat.y;
            z = (float)Quat.z;
            s = (float)Quat.s;
            if (x == 0 && y == 0 && z == 0 && s == 0)
                s = 1;
        }

        public lsl_rotation(double X, double Y, double Z, double S)
        {
            x = X;
            y = Y;
            z = Z;
            s = S;
            if (x == 0 && y == 0 && z == 0 && s == 0)
                s = 1;
        }

        public lsl_rotation(string str)
        {
            str = str.Replace('<', ' ');
            str = str.Replace('>', ' ');
            string[] tmps = str.Split(new Char[] { ',', '<', '>' });
            if (tmps.Length < 4)
            {
                x = y = z = s = 0;
                return;
            }
            bool res;
            res = Double.TryParse(tmps[0], NumberStyles.Float, Utils.EnUsCulture, out x);
            res = res & Double.TryParse(tmps[1], NumberStyles.Float, Utils.EnUsCulture, out y);
            res = res & Double.TryParse(tmps[2], NumberStyles.Float, Utils.EnUsCulture, out z);
            res = res & Double.TryParse(tmps[3], NumberStyles.Float, Utils.EnUsCulture, out s);
            if (x == 0 && y == 0 && z == 0 && s == 0)
                s = 1;
        }

        #endregion

        #region Overriders

        public override int GetHashCode()
        {
            return (x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode() ^ s.GetHashCode());
        }

        public override bool Equals(object o)
        {
            if (!(o is lsl_rotation)) return false;

            lsl_rotation quaternion = (lsl_rotation)o;

            return x == quaternion.x && y == quaternion.y && z == quaternion.z && s == quaternion.s;
        }

        public override string ToString()
        {
            string st = String.Format(Utils.EnUsCulture, "<{0:0.000000}, {1:0.000000}, {2:0.000000}, {3:0.000000}>", x, y, z, s);
            return st;
        }

        public static explicit operator string(lsl_rotation r)
        {
            return r.ToString();
        }

        public static explicit operator lsl_string(lsl_rotation r)
        {
            return new lsl_string(r.ToString());
        }

        public static explicit operator lsl_rotation(string s)
        {
            return new lsl_rotation(s);
        }

        public static implicit operator lsl_list(lsl_rotation r)
        {
            return new lsl_list(new object[] { r });
        }

        public static bool operator ==(lsl_rotation lhs, lsl_rotation rhs)
        {
            // Return true if the fields match:
            return lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z && lhs.s == rhs.s;
        }

        public static bool operator !=(lsl_rotation lhs, lsl_rotation rhs)
        {
            return !(lhs == rhs);
        }

        public static double Mag(lsl_rotation q)
        {
            return Math.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.s * q.s);
        }

        #endregion

        #region Operators

        public static lsl_rotation operator +(lsl_rotation a, lsl_rotation b)
        {
            return new lsl_rotation(a.x + b.x, a.y + b.y, a.z + b.z, a.s + b.s);
        }

        public static lsl_rotation operator /(lsl_rotation a, lsl_rotation b)
        {
            b.s = -b.s;
            return a * b;
        }

        public static lsl_rotation operator -(lsl_rotation a, lsl_rotation b)
        {
            return new lsl_rotation(a.x - b.x, a.y - b.y, a.z - b.z, a.s - b.s);
        }

        // using the equations below, we need to do "b * a" to be compatible with LSL
        public static lsl_rotation operator *(lsl_rotation b, lsl_rotation a)
        {
            lsl_rotation c;
            c.x = a.s * b.x + a.x * b.s + a.y * b.z - a.z * b.y;
            c.y = a.s * b.y + a.y * b.s + a.z * b.x - a.x * b.z;
            c.z = a.s * b.z + a.z * b.s + a.x * b.y - a.y * b.x;
            c.s = a.s * b.s - a.x * b.x - a.y * b.y - a.z * b.z;
            return c;
        }

        public static implicit operator Quaternion(lsl_rotation rotation)
        {
            return new Quaternion((float)rotation.x, (float)rotation.y, (float)rotation.z, (float)rotation.s);
        }

        public static implicit operator lsl_rotation(Quaternion q)
        {
            return new lsl_rotation(q.X, q.Y, q.Z, q.W);
        }

        #endregion Operators

        public static readonly lsl_rotation Identity = new lsl_rotation(0d, 0d, 0d, 1d);
    }

    [Serializable]
    public class lsl_list
    {
        private object[] m_data;

        public lsl_list(params object[] args)
        {
            m_data = new object[args.Length];
            m_data = args;
        }

        public int Length
        {
            get
            {
                if (m_data == null)
                    m_data = new Object[0];
                return m_data.Length;
            }
        }

        public int Size
        {
            get
            {
                if (m_data == null)
                    m_data = new Object[0];

                int size = 0;

                foreach (Object o in m_data)
                {
                    if (o is lsl_integer)
                        size += 4;
                    else if (o is lsl_float)
                        size += 8;
                    else if (o is lsl_string)
                        size += ((lsl_string)o).m_string.Length;
                    else if (o is lsl_key)
                        size += ((lsl_key)o).value.Length;
                    else if (o is lsl_vector)
                        size += 32;
                    else if (o is lsl_rotation)
                        size += 64;
                    else if (o is int)
                        size += 4;
                    else if (o is string)
                        size += ((string)o).Length;
                    else if (o is float)
                        size += 8;
                    else if (o is double)
                        size += 16;
                    else
                        throw new Exception("Unknown type in List.Size: " + o.GetType().ToString());
                }
                return size;
            }
        }

        public object[] Data
        {
            get
            {
                if (m_data == null)
                    m_data = new Object[0];
                return m_data;
            }

            set { m_data = value; }
        }
        // Function to obtain LSL type from an index. This is needed
        // because LSL lists allow for multiple types, and safely
        // iterating in them requires a type check.
        public Type GetLSLListItemType(int itemIndex)
        {
            return m_data[itemIndex].GetType();
        }

        // Member functions to obtain item as specific types.
        // For cases where implicit conversions would apply if items
        // were not in a list (e.g. integer to float, but not float
        // to integer) functions check for alternate types so as to
        // down-cast from Object to the correct type.
        // Note: no checks for item index being valid are performed

        public lsl_float GetLSLFloatItem(int itemIndex)
        {
            if (m_data[itemIndex] is lsl_integer)
            {
                return (lsl_integer)m_data[itemIndex];
            }
            else if (m_data[itemIndex] is int)
            {
                return new lsl_float((int)m_data[itemIndex]);
            }
            else if (m_data[itemIndex] is float)
            {
                return new lsl_float((float)m_data[itemIndex]);
            }
            else if (m_data[itemIndex] is double)
            {
                return new lsl_float((Double)m_data[itemIndex]);
            }
            else if (m_data[itemIndex] is lsl_string)
            {
                return new lsl_float(m_data[itemIndex].ToString());
            }
            else
            {
                return (lsl_float)m_data[itemIndex];
            }
        }

        public lsl_string GetLSLStringItem(int itemIndex)
        {
            if (m_data[itemIndex] is lsl_key)
            {
                return new lsl_string((lsl_key)m_data[itemIndex]);
            }
            else if (m_data[itemIndex] is String)
            {
                return new lsl_string((string)m_data[itemIndex]);
            }
            else if (m_data[itemIndex] is lsl_float)
            {
                return new lsl_string((lsl_float)m_data[itemIndex]);
            }
            else if (m_data[itemIndex] is lsl_integer)
            {
                return new lsl_string((lsl_integer)m_data[itemIndex]);
            }
            else
            {
                return (lsl_string)m_data[itemIndex];
            }
        }

        public lsl_integer GetLSLIntegerItem(int itemIndex)
        {
            if (m_data[itemIndex] is lsl_integer)
                return (lsl_integer)m_data[itemIndex];
            if (m_data[itemIndex] is lsl_float)
                return new lsl_integer((int)m_data[itemIndex]);
            else if (m_data[itemIndex] is Int32)
                return new lsl_integer((int)m_data[itemIndex]);
            else if (m_data[itemIndex] is lsl_string)
                return new lsl_integer((string)m_data[itemIndex]);
            else
                throw new InvalidCastException();
        }

        public lsl_vector GetLSLVectorItem(int itemIndex)
        {
            return (lsl_vector)m_data[itemIndex];
        }

        public lsl_rotation GetLSLRotationItem(int itemIndex)
        {
            return (lsl_rotation)m_data[itemIndex];
        }

        public lsl_key GetKeyItem(int itemIndex)
        {
            return (lsl_key)m_data[itemIndex];
        }

        public static lsl_list operator +(lsl_list a, lsl_list b)
        {
            object[] tmp;
            tmp = new object[a.Length + b.Length];
            a.Data.CopyTo(tmp, 0);
            b.Data.CopyTo(tmp, a.Length);
            return new lsl_list(tmp);
        }

        private void ExtendAndAdd(object o)
        {
            Array.Resize(ref m_data, Length + 1);
            m_data.SetValue(o, Length - 1);
        }

        public static lsl_list operator +(lsl_list a, lsl_string s)
        {
            a.ExtendAndAdd(s);
            return a;
        }

        public static lsl_list operator +(lsl_list a, lsl_integer i)
        {
            a.ExtendAndAdd(i);
            return a;
        }

        public static lsl_list operator +(lsl_list a, lsl_float d)
        {
            a.ExtendAndAdd(d);
            return a;
        }

        public static bool operator ==(lsl_list a, lsl_list b)
        {
            int la = -1;
            int lb = -1;
            try { la = a.Length; }
            catch (NullReferenceException) { }
            try { lb = b.Length; }
            catch (NullReferenceException) { }

            return la == lb;
        }

        public static bool operator !=(lsl_list a, lsl_list b)
        {
            int la = -1;
            int lb = -1;
            try { la = a.Length; }
            catch (NullReferenceException) { }
            try { lb = b.Length; }
            catch (NullReferenceException) { }

            return la != lb;
        }

        public void Add(object o)
        {
            object[] tmp;
            tmp = new object[m_data.Length + 1];
            m_data.CopyTo(tmp, 0);
            tmp[m_data.Length] = o;
            m_data = tmp;
        }

        public bool Contains(object o)
        {
            bool ret = false;
            foreach (object i in Data)
            {
                if (i == o)
                {
                    ret = true;
                    break;
                }
            }
            return ret;
        }

        public lsl_list DeleteSublist(int start, int end)
        {
            // Not an easy one
            // If start <= end, remove that part
            // if either is negative, count from the end of the array
            // if the resulting start > end, remove all BUT that part

            Object[] ret;

            if (start < 0)
                start = m_data.Length - start;

            if (start < 0)
                start = 0;

            if (end < 0)
                end = m_data.Length - end;
            if (end < 0)
                end = 0;

            if (start > end)
            {
                if (end >= m_data.Length)
                    return new lsl_list(new Object[0]);

                if (start >= m_data.Length)
                    start = m_data.Length - 1;

                return GetSublist(end, start);
            }

            // start >= 0 && end >= 0 here
            if (start >= m_data.Length)
            {
                ret = new Object[m_data.Length];
                Array.Copy(m_data, 0, ret, 0, m_data.Length);

                return new lsl_list(ret);
            }

            if (end >= m_data.Length)
                end = m_data.Length - 1;

            // now, this makes the math easier
            int remove = end + 1 - start;

            ret = new Object[m_data.Length - remove];
            if (ret.Length == 0)
                return new lsl_list(ret);

            int src;
            int dest = 0;

            for (src = 0; src < m_data.Length; src++)
            {
                if (src < start || src > end)
                    ret[dest++] = m_data[src];
            }

            return new lsl_list(ret);
        }

        public lsl_list GetSublist(int start, int end)
        {

            object[] ret;

            // Take care of neg start or end's
            // NOTE that either index may still be negative after
            // adding the length, so we must take additional
            // measures to protect against this. Note also that
            // after normalisation the negative indices are no
            // longer relative to the end of the list.

            if (start < 0)
            {
                start = m_data.Length + start;
            }

            if (end < 0)
            {
                end = m_data.Length + end;
            }

            // The conventional case is start <= end
            // NOTE that the case of an empty list is
            // dealt with by the initial test. Start
            // less than end is taken to be the most
            // common case.

            if (start <= end)
            {

                // Start sublist beyond length
                // Also deals with start AND end still negative
                if (start >= m_data.Length || end < 0)
                {
                    return new lsl_list();
                }

                // Sublist extends beyond the end of the supplied list
                if (end >= m_data.Length)
                {
                    end = m_data.Length - 1;
                }

                // Sublist still starts before the beginning of the list
                if (start < 0)
                {
                    start = 0;
                }

                ret = new object[end - start + 1];

                Array.Copy(m_data, start, ret, 0, end - start + 1);

                return new lsl_list(ret);

            }

            // Deal with the segmented case: 0->end + start->EOL

            else
            {

                lsl_list result = null;

                // If end is negative, then prefix list is empty
                if (end < 0)
                {
                    result = new lsl_list();
                    // If start is still negative, then the whole of
                    // the existing list is returned. This case is
                    // only admitted if end is also still negative.
                    if (start < 0)
                    {
                        return this;
                    }

                }
                else
                {
                    result = GetSublist(0, end);
                }

                // If start is outside of list, then just return
                // the prefix, whatever it is.
                if (start >= m_data.Length)
                {
                    return result;
                }

                return result + GetSublist(start, Data.Length);

            }
        }

        private static int compare(object left, object right, int ascending)
        {
            if (!left.GetType().Equals(right.GetType()))
            {
                // unequal types are always "equal" for comparison purposes.
                // this way, the bubble sort will never swap them, and we'll
                // get that feathered effect we're looking for
                return 0;
            }

            int ret = 0;

            if (left is lsl_key)
            {
                lsl_key l = (lsl_key)left;
                lsl_key r = (lsl_key)right;
                ret = String.CompareOrdinal(l.value, r.value);
            }
            else if (left is lsl_string)
            {
                lsl_string l = (lsl_string)left;
                lsl_string r = (lsl_string)right;
                ret = String.CompareOrdinal(l.m_string, r.m_string);
            }
            else if (left is lsl_integer)
            {
                lsl_integer l = (lsl_integer)left;
                lsl_integer r = (lsl_integer)right;
                ret = Math.Sign(l.value - r.value);
            }
            else if (left is lsl_float)
            {
                lsl_float l = (lsl_float)left;
                lsl_float r = (lsl_float)right;
                ret = Math.Sign(l.value - r.value);
            }
            else if (left is lsl_vector)
            {
                lsl_vector l = (lsl_vector)left;
                lsl_vector r = (lsl_vector)right;
                ret = Math.Sign(lsl_vector.Mag(l) - lsl_vector.Mag(r));
            }
            else if (left is lsl_rotation)
            {
                lsl_rotation l = (lsl_rotation)left;
                lsl_rotation r = (lsl_rotation)right;
                ret = Math.Sign(lsl_rotation.Mag(l) - lsl_rotation.Mag(r));
            }

            if (ascending == 0)
            {
                ret = 0 - ret;
            }

            return ret;
        }

        class HomogeneousComparer : IComparer
        {
            public HomogeneousComparer()
            {
            }

            public int Compare(object lhs, object rhs)
            {
                return compare(lhs, rhs, 1);
            }
        }

        public lsl_list Sort(int stride, int ascending)
        {
            if (Data.Length == 0)
                return new lsl_list(); // Don't even bother

            object[] ret = new object[Data.Length];
            Array.Copy(Data, 0, ret, 0, Data.Length);

            if (stride <= 0)
            {
                stride = 1;
            }

            // we can optimize here in the case where stride == 1 and the list
            // consists of homogeneous types

            if (stride == 1)
            {
                bool homogeneous = true;
                int index;
                for (index = 1; index < Data.Length; index++)
                {
                    if (!Data[0].GetType().Equals(Data[index].GetType()))
                    {
                        homogeneous = false;
                        break;
                    }
                }

                if (homogeneous)
                {
                    Array.Sort(ret, new HomogeneousComparer());
                    if (ascending == 0)
                    {
                        Array.Reverse(ret);
                    }
                    return new lsl_list(ret);
                }
            }

            // Because of the desired type specific feathered sorting behavior
            // requried by the spec, we MUST use a non-optimized bubble sort here.
            // Anything else will give you the incorrect behavior.

            // begin bubble sort...
            int i;
            int j;
            int k;
            int n = Data.Length;

            for (i = 0; i < (n - stride); i += stride)
            {
                for (j = i + stride; j < n; j += stride)
                {
                    if (compare(ret[i], ret[j], ascending) > 0)
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

            // end bubble sort

            return new lsl_list(ret);
        }

        #region CSV Methods

        public static lsl_list FromCSV(string csv)
        {
            return new lsl_list(csv.Split(','));
        }

        public string ToCSV()
        {
            string ret = "";
            foreach (object o in this.Data)
            {
                if (ret == "")
                {
                    ret = o.ToString();
                }
                else
                {
                    ret = ret + ", " + o.ToString();
                }
            }
            return ret;
        }

        private string ToSoup()
        {
            string output;
            output = String.Empty;
            if (m_data.Length == 0)
            {
                return String.Empty;
            }
            foreach (object o in m_data)
            {
                output = output + o.ToString();
            }
            return output;
        }

        public static explicit operator String(lsl_list l)
        {
            return l.ToSoup();
        }

        public static explicit operator lsl_string(lsl_list l)
        {
            return new lsl_string(l.ToSoup());
        }

        public override string ToString()
        {
            return ToSoup();
        }

        #endregion

        #region Statistic Methods

        public double Min()
        {
            double minimum = double.PositiveInfinity;
            double entry;
            for (int i = 0; i < Data.Length; i++)
            {
                if (double.TryParse(Data[i].ToString(), out entry))
                {
                    if (entry < minimum) minimum = entry;
                }
            }
            return minimum;
        }

        public double Max()
        {
            double maximum = double.NegativeInfinity;
            double entry;
            for (int i = 0; i < Data.Length; i++)
            {
                if (double.TryParse(Data[i].ToString(), out entry))
                {
                    if (entry > maximum) maximum = entry;
                }
            }
            return maximum;
        }

        public double Range()
        {
            return (this.Max() / this.Min());
        }

        public int NumericLength()
        {
            int count = 0;
            double entry;
            for (int i = 0; i < Data.Length; i++)
            {
                if (double.TryParse(Data[i].ToString(), out entry))
                {
                    count++;
                }
            }
            return count;
        }

        public static lsl_list ToDoubleList(lsl_list src)
        {
            lsl_list ret = new lsl_list();
            double entry;
            for (int i = 0; i < src.Data.Length - 1; i++)
            {
                if (double.TryParse(src.Data[i].ToString(), out entry))
                {
                    ret.Add(entry);
                }
            }
            return ret;
        }

        public double Sum()
        {
            double sum = 0;
            double entry;
            for (int i = 0; i < Data.Length; i++)
            {
                if (double.TryParse(Data[i].ToString(), out entry))
                {
                    sum = sum + entry;
                }
            }
            return sum;
        }

        public double SumSqrs()
        {
            double sum = 0;
            double entry;
            for (int i = 0; i < Data.Length; i++)
            {
                if (double.TryParse(Data[i].ToString(), out entry))
                {
                    sum = sum + Math.Pow(entry, 2);
                }
            }
            return sum;
        }

        public double Mean()
        {
            return (this.Sum() / this.NumericLength());
        }

        public void NumericSort()
        {
            IComparer Numeric = new NumericComparer();
            Array.Sort(Data, Numeric);
        }

        public void AlphaSort()
        {
            IComparer Alpha = new AlphaCompare();
            Array.Sort(Data, Alpha);
        }

        public double Median()
        {
            return Qi(0.5);
        }

        public double GeometricMean()
        {
            double ret = 1.0;
            lsl_list nums = ToDoubleList(this);
            for (int i = 0; i < nums.Data.Length; i++)
            {
                ret *= (double)nums.Data[i];
            }
            return Math.Exp(Math.Log(ret) / (double)nums.Data.Length);
        }

        public double HarmonicMean()
        {
            double ret = 0.0;
            lsl_list nums = ToDoubleList(this);
            for (int i = 0; i < nums.Data.Length; i++)
            {
                ret += 1.0 / (double)nums.Data[i];
            }
            return ((double)nums.Data.Length / ret);
        }

        public double Variance()
        {
            double s = 0;
            lsl_list num = ToDoubleList(this);
            for (int i = 0; i < num.Data.Length; i++)
            {
                s += Math.Pow((double)num.Data[i], 2);
            }
            return (s - num.Data.Length * Math.Pow(num.Mean(), 2)) / (num.Data.Length - 1);
        }

        public double StdDev()
        {
            return Math.Sqrt(this.Variance());
        }

        public double Qi(double i)
        {
            lsl_list j = this;
            j.NumericSort();

            if (Math.Ceiling(this.Length * i) == this.Length * i)
            {
                return (double)((double)j.Data[(int)(this.Length * i - 1)] + (double)j.Data[(int)(this.Length * i)]) / 2;
            }
            else
            {
                return (double)j.Data[((int)(Math.Ceiling(this.Length * i))) - 1];
            }
        }

        #endregion

        public string ToPrettyString()
        {
            string output;
            if (m_data.Length == 0)
            {
                return "[]";
            }
            output = "[";
            foreach (object o in m_data)
            {
                if (o is String)
                {
                    output = output + "\"" + o + "\", ";
                }
                else
                {
                    output = output + o.ToString() + ", ";
                }
            }
            output = output.Substring(0, output.Length - 2);
            output = output + "]";
            return output;
        }

        public class AlphaCompare : IComparer
        {
            int IComparer.Compare(object x, object y)
            {
                return string.Compare(x.ToString(), y.ToString());
            }
        }

        public class NumericComparer : IComparer
        {
            int IComparer.Compare(object x, object y)
            {
                double a;
                double b;
                if (!double.TryParse(x.ToString(), out a))
                {
                    a = 0.0;
                }
                if (!double.TryParse(y.ToString(), out b))
                {
                    b = 0.0;
                }
                if (a < b)
                {
                    return -1;
                }
                else if (a == b)
                {
                    return 0;
                }
                else
                {
                    return 1;
                }
            }
        }

        public override bool Equals(object o)
        {
            if (!(o is lsl_list))
                return false;

            return Data.Length == ((lsl_list)o).Data.Length;
        }

        public override int GetHashCode()
        {
            return Data.GetHashCode();
        }
    }

    [Serializable]
    public struct lsl_key
    {
        public string value;

        #region Constructors
        public lsl_key(string s)
        {
            value = s;
        }

        #endregion

        #region Methods

        public static bool Parse2Key(string s)
        {
            Regex isuuid = new Regex(@"^[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}$", RegexOptions.Compiled);
            if (isuuid.IsMatch(s))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region Operators

        public static implicit operator Boolean(lsl_key k)
        {
            if (k.value.Length == 0)
            {
                return false;
            }

            if (k.value == "00000000-0000-0000-0000-000000000000")
            {
                return false;
            }
            Regex isuuid = new Regex(@"^[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}$", RegexOptions.Compiled);
            if (isuuid.IsMatch(k.value))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static implicit operator lsl_key(string s)
        {
            return new lsl_key(s);
        }

        public static implicit operator lsl_key(lsl_string s)
        {
            return new lsl_key(s.m_string);
        }

        public static implicit operator lsl_key(UUID u)
        {
            return new lsl_key(u.ToString());
        }

        public static implicit operator UUID(lsl_key k)
        {
            UUID id;
            UUID.TryParse(k.value, out id);
            return id;
        }

        public static implicit operator String(lsl_key k)
        {
            return k.value;
        }

        //public static implicit operator lsl_string(lsl_key k)
        //{
        //    return k.value;
        //}

        public static bool operator ==(lsl_key k1, lsl_key k2)
        {
            return k1.value == k2.value;
        }

        public static bool operator !=(lsl_key k1, lsl_key k2)
        {
            return k1.value != k2.value;
        }

        #endregion

        #region Overriders

        public override bool Equals(object o)
        {
            return o.ToString() == value;
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override string ToString()
        {
            return value;
        }

        #endregion

        public static readonly lsl_key Zero = new lsl_key("00000000-0000-0000-0000-000000000000");
        public static readonly lsl_key Empty = new lsl_key(String.Empty);
    }

    [Serializable]
    public struct lsl_string
    {
        public string m_string;
        #region Constructors
        public lsl_string(string s)
        {
            m_string = s;
        }

        public lsl_string(double d)
        {
            string s = String.Format(Utils.EnUsCulture, "{0:0.000000}", d);
            m_string = s;
        }

        public lsl_string(lsl_float f)
        {
            string s = String.Format(Utils.EnUsCulture, "{0:0.000000}", f.value);
            m_string = s;
        }

        public lsl_string(lsl_integer i)
        {
            string s = String.Format("{0}", i);
            m_string = s;
        }

        #endregion

        #region Operators
        public static implicit operator Boolean(lsl_string s)
        {
            if (s.m_string.Length == 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static implicit operator String(lsl_string s)
        {
            return s.m_string;
        }

        public static implicit operator lsl_string(string s)
        {
            return new lsl_string(s);
        }

        public static string ToString(lsl_string s)
        {
            return s.m_string;
        }

        public override string ToString()
        {
            return m_string;
        }

        public static bool operator ==(lsl_string s1, string s2)
        {
            return s1.m_string == s2;
        }

        public static bool operator !=(lsl_string s1, string s2)
        {
            return s1.m_string != s2;
        }

        public static lsl_string operator +(lsl_string s1, lsl_string s2)
        {
            return new lsl_string(s1.m_string + s2.m_string);
        }

        public static explicit operator double(lsl_string s)
        {
            return new lsl_float(s).value;
        }

        public static explicit operator lsl_integer(lsl_string s)
        {
            return new lsl_integer(s.m_string);
        }

        public static explicit operator lsl_string(double d)
        {
            return new lsl_string(d);
        }

        public static explicit operator lsl_string(lsl_float f)
        {
            return new lsl_string(f);
        }

        public static explicit operator lsl_string(bool b)
        {
            if (b)
                return new lsl_string("1");
            else
                return new lsl_string("0");
        }

        public static explicit operator lsl_string(Vector3 v)
        {
            return new lsl_string(new lsl_vector(v).ToString());
        }

        public static explicit operator lsl_string(Quaternion q)
        {
            return new lsl_string(new lsl_rotation(q).ToString());
        }

        public static explicit operator lsl_string(Int32 i)
        {
            return new lsl_string(i);
        }

        public static explicit operator lsl_string(lsl_key k)
        {
            return new lsl_string(k);
        }

        public static implicit operator lsl_vector(lsl_string s)
        {
            return new lsl_vector(s.m_string);
        }

        public static implicit operator lsl_rotation(lsl_string s)
        {
            return new lsl_rotation(s.m_string);
        }

        public static implicit operator lsl_float(lsl_string s)
        {
            return new lsl_float(s.m_string);
        }

        public static implicit operator lsl_list(lsl_string s)
        {
            return new lsl_list(new object[] { s });
        }

        #endregion

        #region Overriders
        public override bool Equals(object o)
        {
            return m_string == o.ToString();
        }

        public override int GetHashCode()
        {
            return m_string.GetHashCode();
        }

        #endregion

        #region Standard string functions

        //Clone,CompareTo,Contains
        //CopyTo,EndsWith,Equals,GetEnumerator,GetHashCode,GetType,GetTypeCode
        //IndexOf,IndexOfAny,Insert,IsNormalized,LastIndexOf,LastIndexOfAny
        //Length,Normalize,PadLeft,PadRight,Remove,Replace,Split,StartsWith,Substring,ToCharArray,ToLowerInvariant
        //ToString,ToUpper,ToUpperInvariant,Trim,TrimEnd,TrimStart
        public bool Contains(string value) { return m_string.Contains(value); }
        public int IndexOf(string value) { return m_string.IndexOf(value); }
        public int Length { get { return m_string.Length; } }

        #endregion

        public static readonly lsl_string Empty = new lsl_string(String.Empty);
    }

    [Serializable]
    public struct lsl_integer
    {
        public int value;

        #region Constructors

        public lsl_integer(int i)
        {
            value = i;
        }

        public lsl_integer(uint i)
        {
            value = (int)i;
        }

        public lsl_integer(double d)
        {
            value = (int)d;
        }

        public lsl_integer(string s)
        {
            Regex r = new Regex("(^[ ]*0[xX][0-9A-Fa-f][0-9A-Fa-f]*)|(^[ ]*-?[0-9][0-9]*)");
            Match m = r.Match(s);
            string v = m.Groups[0].Value;

            if (v == String.Empty)
            {
                value = 0;
            }
            else
            {
                try
                {
                    if (v.Contains("x") || v.Contains("X"))
                    {
                        value = int.Parse(v.Substring(2), System.Globalization.NumberStyles.HexNumber);
                    }
                    else
                    {
                        value = int.Parse(v, System.Globalization.NumberStyles.Integer);
                    }
                }
                catch (OverflowException)
                {
                    value = -1;
                }
            }
        }

        #endregion

        #region Operators

        public static implicit operator int(lsl_integer i)
        {
            return i.value;
        }

        public static explicit operator uint(lsl_integer i)
        {
            return (uint)i.value;
        }

        public static explicit operator lsl_string(lsl_integer i)
        {
            return new lsl_string(i.ToString());
        }

        public static implicit operator lsl_list(lsl_integer i)
        {
            return new lsl_list(new object[] { i });
        }

        public static implicit operator Boolean(lsl_integer i)
        {
            if (i.value == 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static implicit operator lsl_integer(int i)
        {
            return new lsl_integer(i);
        }

        public static explicit operator lsl_integer(string s)
        {
            return new lsl_integer(s);
        }

        public static implicit operator lsl_integer(uint u)
        {
            return new lsl_integer(u);
        }

        public static explicit operator lsl_integer(double d)
        {
            return new lsl_integer(d);
        }

        public static explicit operator lsl_integer(lsl_float f)
        {
            return new lsl_integer(f.value);
        }

        public static implicit operator lsl_integer(bool b)
        {
            if (b)
                return new lsl_integer(1);
            else
                return new lsl_integer(0);
        }

        public static lsl_integer operator ==(lsl_integer i1, lsl_integer i2)
        {
            bool ret = i1.value == i2.value;
            return new lsl_integer((ret ? 1 : 0));
        }

        public static lsl_integer operator !=(lsl_integer i1, lsl_integer i2)
        {
            bool ret = i1.value != i2.value;
            return new lsl_integer((ret ? 1 : 0));
        }

        public static lsl_integer operator <(lsl_integer i1, lsl_integer i2)
        {
            bool ret = i1.value < i2.value;
            return new lsl_integer((ret ? 1 : 0));
        }
        public static lsl_integer operator <=(lsl_integer i1, lsl_integer i2)
        {
            bool ret = i1.value <= i2.value;
            return new lsl_integer((ret ? 1 : 0));
        }

        public static lsl_integer operator >(lsl_integer i1, lsl_integer i2)
        {
            bool ret = i1.value > i2.value;
            return new lsl_integer((ret ? 1 : 0));
        }

        public static lsl_integer operator >=(lsl_integer i1, lsl_integer i2)
        {
            bool ret = i1.value >= i2.value;
            return new lsl_integer((ret ? 1 : 0));
        }

        public static lsl_integer operator +(lsl_integer i1, int i2)
        {
            return new lsl_integer(i1.value + i2);
        }

        public static lsl_integer operator -(lsl_integer i1, int i2)
        {
            return new lsl_integer(i1.value - i2);
        }

        public static lsl_integer operator *(lsl_integer i1, int i2)
        {
            return new lsl_integer(i1.value * i2);
        }

        public static lsl_integer operator /(lsl_integer i1, int i2)
        {
            return new lsl_integer(i1.value / i2);
        }

        public static lsl_integer operator -(lsl_integer i)
        {
            return new lsl_integer(-i.value);
        }

        public static lsl_integer operator ~(lsl_integer i)
        {
            return new lsl_integer(~i.value);
        }

        public override bool Equals(Object o)
        {
            if (!(o is lsl_integer))
                return false;
            return value == ((lsl_integer)o).value;
        }

        public override int GetHashCode()
        {
            return value;
        }

        public static lsl_integer operator &(lsl_integer i1, lsl_integer i2)
        {
            int ret = i1.value & i2.value;
            return ret;
        }

        public static lsl_integer operator %(lsl_integer i1, lsl_integer i2)
        {
            int ret = i1.value % i2.value;
            return ret;
        }

        public static lsl_integer operator |(lsl_integer i1, lsl_integer i2)
        {
            int ret = i1.value | i2.value;
            return ret;
        }

        public static lsl_integer operator ^(lsl_integer i1, lsl_integer i2)
        {
            int ret = i1.value ^ i2.value;
            return ret;
        }

        public static lsl_integer operator !(lsl_integer i1)
        {
            return i1.value == 0 ? 1 : 0;
        }

        public static lsl_integer operator ++(lsl_integer i)
        {
            i.value++;
            return i;
        }

        public static lsl_integer operator --(lsl_integer i)
        {
            i.value--;
            return i;
        }

        public static lsl_integer operator <<(lsl_integer i, int s)
        {
            return i.value << s;
        }

        public static lsl_integer operator >>(lsl_integer i, int s)
        {
            return i.value >> s;
        }

        public static implicit operator System.Double(lsl_integer i)
        {
            return (double)i.value;
        }

        public static bool operator true(lsl_integer i)
        {
            return i.value != 0;
        }

        public static bool operator false(lsl_integer i)
        {
            return i.value == 0;
        }

        #endregion

        #region Overriders

        public override string ToString()
        {
            return this.value.ToString();
        }

        #endregion

        public static readonly lsl_integer Zero = new lsl_integer();
    }

    [Serializable]
    public struct lsl_float
    {
        public double value;

        #region Constructors

        public lsl_float(int i)
        {
            this.value = (double)i;
        }

        public lsl_float(double d)
        {
            this.value = d;
        }

        public lsl_float(string s)
        {
            Regex r = new Regex("^ *(\\+|-)?([0-9]+\\.?[0-9]*|\\.[0-9]+)([eE](\\+|-)?[0-9]+)?");
            Match m = r.Match(s);
            string v = m.Groups[0].Value;

            v = v.Trim();

            if (v == String.Empty || v == null)
                v = "0.0";
            else
                if (!v.Contains(".") && !v.ToLower().Contains("e"))
                    v = v + ".0";
                else
                    if (v.EndsWith("."))
                        v = v + "0";
            this.value = double.Parse(v, System.Globalization.NumberStyles.Float, Utils.EnUsCulture);
        }

        #endregion

        #region Operators

        public static explicit operator float(lsl_float f)
        {
            return (float)f.value;
        }

        public static explicit operator int(lsl_float f)
        {
            return (int)f.value;
        }

        public static explicit operator uint(lsl_float f)
        {
            return (uint)Math.Abs(f.value);
        }

        public static implicit operator Boolean(lsl_float f)
        {
            if (f.value == 0.0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static implicit operator lsl_float(int i)
        {
            return new lsl_float(i);
        }

        public static implicit operator lsl_float(lsl_integer i)
        {
            return new lsl_float(i.value);
        }

        public static explicit operator lsl_float(string s)
        {
            return new lsl_float(s);
        }

        public static implicit operator lsl_list(lsl_float f)
        {
            return new lsl_list(new object[] { f });
        }

        public static implicit operator lsl_float(double d)
        {
            return new lsl_float(d);
        }

        public static implicit operator lsl_float(bool b)
        {
            if (b)
                return new lsl_float(1.0);
            else
                return new lsl_float(0.0);
        }

        public static bool operator ==(lsl_float f1, lsl_float f2)
        {
            return f1.value == f2.value;
        }

        public static bool operator !=(lsl_float f1, lsl_float f2)
        {
            return f1.value != f2.value;
        }

        public static lsl_float operator ++(lsl_float f)
        {
            f.value++;
            return f;
        }

        public static lsl_float operator --(lsl_float f)
        {
            f.value--;
            return f;
        }

        public static lsl_float operator +(lsl_float f, int i)
        {
            return new lsl_float(f.value + (double)i);
        }

        public static lsl_float operator -(lsl_float f, int i)
        {
            return new lsl_float(f.value - (double)i);
        }

        public static lsl_float operator *(lsl_float f, int i)
        {
            return new lsl_float(f.value * (double)i);
        }

        public static lsl_float operator /(lsl_float f, int i)
        {
            return new lsl_float(f.value / (double)i);
        }

        public static lsl_float operator +(lsl_float lhs, lsl_float rhs)
        {
            return new lsl_float(lhs.value + rhs.value);
        }

        public static lsl_float operator -(lsl_float lhs, lsl_float rhs)
        {
            return new lsl_float(lhs.value - rhs.value);
        }

        public static lsl_float operator *(lsl_float lhs, lsl_float rhs)
        {
            return new lsl_float(lhs.value * rhs.value);
        }

        public static lsl_float operator /(lsl_float lhs, lsl_float rhs)
        {
            return new lsl_float(lhs.value / rhs.value);
        }

        public static lsl_float operator -(lsl_float f)
        {
            return new lsl_float(-f.value);
        }

        public static implicit operator double(lsl_float f)
        {
            return f.value;
        }

        #endregion

        #region Overriders

        public override string ToString()
        {
            return String.Format(Utils.EnUsCulture, "{0:0.000000}", this.value);
        }

        public override bool Equals(Object o)
        {
            if (!(o is lsl_float))
                return false;
            return value == ((lsl_float)o).value;
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }


        #endregion

        public static readonly lsl_float Zero = new lsl_float();
    }
}
