using System;
using System.Collections.Generic;
using System.Globalization;
using OpenMetaverse;

namespace Simian.Scripting.Linden
{
    /// <summary>
    /// Provides common helper methods for LSL API functions
    /// </summary>
    public static class LSLUtils
    {
        private static HashSet<char> ALLOWED_INT_CHARS = new HashSet<char>
        { ' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-' };
        private static HashSet<char> ALLOWED_FLOAT_CHARS = new HashSet<char>
        { ' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', '.', 'E', 'e' };

        public static string ObjectToString(object o)
        {
            if (o is float)
            {
                return FloatToString6((float)o);
            }
            else if (o is Vector3)
            {
                Vector3 v = (Vector3)o;
                return String.Format(Utils.EnUsCulture, "<{0}, {1}, {2}>",
                    FloatToString6(v.X),
                    FloatToString6(v.Y),
                    FloatToString6(v.Z));
            }
            else if (o is Quaternion)
            {
                Quaternion q = (Quaternion)o;
                return String.Format(Utils.EnUsCulture, "<{0}, {1}, {2}, {3}>",
                    FloatToString6(q.X),
                    FloatToString6(q.Y),
                    FloatToString6(q.Z),
                    FloatToString6(q.W));
            }
            else
            {
                return o.ToString();
            }
        }

        /// <summary>
        /// Tests if a floating point value is equal to -0.0
        /// </summary>
        /// <param name="f">Float to test</param>
        /// <returns>True if the given value is equal to negative zero, 
        /// otherwise false</returns>
        public static unsafe bool IsNegativeZero(float f)
        {
            int i = *(((int*)&f));
            return i == -2147483648;
        }

        /// <summary>
        /// Converts a floating point value to a string formatted according to 
        /// LSL float->string typecasting rules, including preserving negative 
        /// zero
        /// </summary>
        /// <param name="f">Float to convert to a string</param>
        /// <returns>A string representation of the given value</returns>
        public static string FloatToString6(float f)
        {
            if (IsNegativeZero(f))
                return "-0.000000";
            return String.Format(Utils.EnUsCulture, "{0:0.000000}", f);
        }

        /// <summary>
        /// Tries to convert a string to an integer according to LSL 
        /// string->int typecasting rules, including 0x-prefixed hex numbers 
        /// and ignoring trailing non-numeric characters
        /// </summary>
        /// <param name="s">String to attempt to parse as an int</param>
        /// <param name="i">Resulting integer on success</param>
        /// <returns>True if successful, otherwise false</returns>
        public static bool TryParseInt(string s, out int i)
        {
            if (String.IsNullOrEmpty(s))
            {
                i = 0;
                return false;
            }

            bool isHex = (s.Substring(0, 2).ToLowerInvariant() == "0x");
            if (isHex)
                s = s.Substring(2);

            int len = 0;
            while (len < s.Length && ALLOWED_INT_CHARS.Contains(s[len]))
                ++len;
            s = s.Substring(0, len);

            if (isHex)
                return Int32.TryParse(s, NumberStyles.AllowHexSpecifier, Utils.EnUsCulture.NumberFormat, out i);
            else
                return Int32.TryParse(s, NumberStyles.Integer, Utils.EnUsCulture.NumberFormat, out i);
        }

        /// <summary>
        /// Tries to convert a string to a floating point value according to
        /// LSL string->float typecasting rules, including ignoring trailing 
        /// non-numeric characters
        /// </summary>
        /// <param name="s">String to attempt to parse as a float</param>
        /// <param name="f">Resulting float value on success</param>
        /// <returns>True if successful, otherwise false</returns>
        public static bool TryParseFloat(string s, out float f)
        {
            if (String.IsNullOrEmpty(s))
            {
                f = 0f;
                return false;
            }

            int len = 0;
            while (len < s.Length && ALLOWED_FLOAT_CHARS.Contains(s[len]))
                ++len;
            s = s.Substring(0, len);

            if (Single.TryParse(s, NumberStyles.Float, Utils.EnUsCulture.NumberFormat, out f))
            {
                // Single.TryParse doesn't handle -0.0 properly so we need to handle it here
                if (s.Trim().StartsWith("-"))
                    f *= -1.0f;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to convert a string to a Vector3 value according to LSL 
        /// string->vector typecasting rules, including all of the rules for
        /// typecasting string->float
        /// </summary>
        /// <param name="s">String to attempt to parse as a vector</param>
        /// <param name="v">Resulting vector value on success</param>
        /// <returns>True if successful, otherwise false</returns>
        public static bool TryParseVector3(string s, out Vector3 v)
        {
            string[] split = s.Replace("<", String.Empty).Replace(">", String.Empty).Split(',');
            float x, y, z;

            if (split.Length == 3 &&
                TryParseFloat(split[0], out x) &&
                TryParseFloat(split[1], out y) &&
                TryParseFloat(split[2], out z))
            {
                v = new Vector3(x, y, z);
                return true;
            }

            v = Vector3.Zero;
            return false;
        }

        /// <summary>
        /// Tries to convert a string to a Quaternion value according to LSL 
        /// string->rotation typecasting rules, including all of the rules for
        /// typecasting string->float
        /// </summary>
        /// <param name="s">String to attempt to parse as a rotation</param>
        /// <param name="q">Resulting rotation value on success</param>
        /// <returns>True if successful, otherwise false</returns>
        public static bool TryParseQuaternion(string s, out Quaternion q)
        {
            string[] split = s.Replace("<", String.Empty).Replace(">", String.Empty).Split(',');
            float x, y, z, w;

            if (split.Length == 4 &&
                TryParseFloat(split[0], out x) &&
                TryParseFloat(split[1], out y) &&
                TryParseFloat(split[2], out z) &&
                TryParseFloat(split[3], out w))
            {
                q = new Quaternion(x, y, z, w);
                return true;
            }

            q = Quaternion.Identity;
            return false;
        }
    }
}
