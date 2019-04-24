using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;
using Npgsql;

namespace Fetcho.Common
{
    /// <summary>
    /// Extension methods for various types
    /// </summary>
    public static class TypeExtensions
    {
        static Random random = new Random(DateTime.UtcNow.Millisecond);

        /// <summary>
        /// Appends another array to this array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array1"></param>
        /// <param name="array2"></param>
        /// <returns></returns>
        public static T[] Append<T>(this T[] array1, params T[] array2)
        {
            if (array1 == null) throw new ArgumentNullException("array1");
            if (array2 == null) throw new ArgumentNullException("array2");

            var c = new T[array1.Length + array2.Length];
            Array.Copy(array1, 0, c, 0, array1.Length);
            Array.Copy(array2, 0, c, array1.Length, array2.Length);
            return c;
        }

        /// <summary>
        /// Reverses the array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array1"></param>
        /// <returns></returns>
        public static T[] Reverse<T>(this T[] array1)
        {
            if (array1 == null) throw new ArgumentNullException("array1");

            var l = array1.Length - 1;
            var b = new T[array1.Length];
            foreach (var c in array1)
                b[l--] = c;
            return b;
        }

        /// <summary>
        /// Returns a subset of array1
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array1"></param>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static T[] Subset<T>(this T[] array1, int startIndex, int length)
        {
            if (array1 == null) throw new ArgumentNullException("array1");
            if (startIndex < 0 || startIndex >= array1.Length) throw new ArgumentException("startIndex must be greater than zero and less than array1 length");
            if (startIndex + length > array1.Length) throw new ArgumentException("startIndex + length must be less than the total length of array1");

            var b = new T[length];
            Array.Copy(array1, startIndex, b, 0, length);
            return b;
        }

        /// <summary>
        /// Left pad the array to make it match the number of items required
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array1"></param>
        /// <param name="paddingValue"></param>
        /// <param name="number"></param>
        /// <returns></returns>
        public static T[] PadLeft<T>(this T[] array1, T paddingValue, int number)
        {
            if (array1.Length >= number)
                return array1;

            T[] c = new T[number];
            Array.Copy(array1, 0, c, number - array1.Length, array1.Length);
            for (int i = 0; i < number - array1.Length; i++)
                c[i] = paddingValue;

            return c;
        }

        /// <summary>
        /// Pad the array to make it match the number of items required
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array1"></param>
        /// <param name="paddingValue"></param>
        /// <param name="number"></param>
        /// <returns></returns>
        public static T[] PadRight<T>(this T[] array1, T paddingValue, int number)
        {
            if (array1.Length >= number)
                return array1;

            T[] c = new T[number];
            Array.Copy(array1, 0, c, 0, array1.Length);
            for (int i = 0; i < number - array1.Length; i++)
                c[array1.Length + i] = paddingValue;

            return c;
        }

        /// <summary>
        /// Remove X number of chars off the end of a string
        /// </summary>
        /// <param name="str"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string Chop(this string str, int numchars = 1)
        {
            if (numchars > str.Length)
                throw new ArgumentException("numchars has to be less than the length of the string");
            return str.Substring(0, str.Length - numchars);
        }

        public static int ConstrainMax(this int val, int maxValue) => val > maxValue ? maxValue : val;  
        public static int ConstrainMin(this int val, int minValue) => val < minValue ? minValue : val;
        public static int ConstrainRange(this int val, int min, int max) => val < min ? min : val > max ? max : val;
        public static bool IsBetween(this int val, int min, int max) => val >= min && val <= max;

        public static long ConstrainMax(this long val, long max) => val > max ? max : val;
        public static long ConstrainMin(this long val, long min) => val < min ? min : val;
        public static long ConstrainRange(this long val, long min, long max) => val < min ? min : val > max ? max : val;
        public static bool IsBetween(this long val, long min, long max) => val >= min && val <= max;

        public static double ConstrainMax(this double val, double max) => val > max ? max : val;
        public static double ConstrainMin(this double val, double min) => val < min ? min : val;
        public static double ConstrainRange(this double val, double min, double max) => val < min ? min : val > max ? max : val;
        public static bool IsBetween(this double val, double min, double max) => val >= min && val <= max;

        public static decimal ConstrainMax(this decimal val, decimal max) => val > max ? max : val;
        public static decimal ConstrainMin(this decimal val, decimal min) => val < min ? min : val;
        public static decimal ConstrainRange(this decimal val, decimal min, decimal max) => val < min ? min : val > max ? max : val;
        public static bool IsBetween(this decimal val, decimal min, decimal max) => val >= min && val <= max;

        public static float ConstrainMax(this float val, float max) => val > max ? max : val;
        public static float ConstrainMin(this float val, float min) => val < min ? min : val;
        public static float ConstrainRange(this float val, float min, float max) => val < min ? min : val > max ? max : val;
        public static bool IsBetween(this float val, float min, float max) => val >= min && val <= max;

        /// <summary>
        /// Ensure a string always only comes to maxlength
        /// </summary>
        /// <param name="value"></param>
        /// <param name="maxlength"></param>
        /// <returns></returns>
        public static string Truncate(this string value, int maxlength)
        {
            if (value.Length < maxlength) return value;
            return value.Substring(0, maxlength);
        }

        /// <summary>
        /// Based on an index, return a some chars before and after that index
        /// </summary>
        /// <param name="value"></param>
        /// <param name="index"></param>
        /// <param name="charsBefore"></param>
        /// <param name="charsAfter"></param>
        /// <returns></returns>
        public static string Fragment(this string value, int index, int charsBefore, int charsAfter )
        {
            if (index < 0) throw new ArgumentException("Invalid string index");
            if (index >= value.Length) throw new ArgumentException("Invalid string index");

            int startIndex = index - charsBefore;
            int endIndex = index + charsAfter;

            if (startIndex < 0) startIndex = 0;
            if (endIndex >= value.Length) endIndex = value.Length;

            return value.Substring(startIndex, endIndex - startIndex);
        }

        /// <summary>
        /// Strip non-XML chars from the provided string
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string CleanupForXml(this string value)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < value.Length; i++)
                if (XmlConvert.IsXmlChar(value[i]))
                    sb.Append(value[i]);

            return sb.ToString();
        }

        /// <summary>
        /// Removes duplicate whitespace from this string
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ReduceWhitespace(this string value)
        {
            if (value.Length == 0) return string.Empty;
            var sb = new StringBuilder();

            sb.Append(value[0]);
            for ( int i=1;i<value.Length;i++)
                if ( !char.IsWhiteSpace(value[i]) || !char.IsWhiteSpace(value[i-1]) )
                    sb.Append(value[i]);

            return sb.ToString();
        }

        public const string CleanInputRegexAlphanumericFilename = @"[^\w-_]";

        /// <summary>
        /// Use this to cleanup input provided from users
        /// </summary>
        /// <param name="value"></param>
        /// <param name="regex"></param>
        /// <returns></returns>
        public static string CleanInput(this string value, string regex = CleanInputRegexAlphanumericFilename)
        {
            // Replace invalid characters with empty strings.
            try
            {
                return Regex.Replace(value, regex, "",
                                     RegexOptions.None, TimeSpan.FromSeconds(1.5));
            }
            // If we timeout when replacing invalid characters, 
            // we should return Empty.
            catch (RegexMatchTimeoutException)
            {
                return string.Empty;
            }
        }

        public async static Task SendOrWaitAsync<T>(this ITargetBlock<T> target, T item, int waitTime = 100)
        {
            while (!await target.SendAsync(item))
                await Task.Delay(waitTime);
        }

        public static IEnumerable<T> Randomise<T>(this IEnumerable<T> items)
            => items.OrderBy(x => random.NextDouble());

        // If you want to implement both "*" and "?"
        private static string WildCardToRegex(this string value)
            => "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";

        public static bool IsWildCardMatch(this string value, string wildCardPattern)
            => Regex.IsMatch(value, wildCardPattern.WildCardToRegex());


        public static TValue SafeGet<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
            => dict.ContainsKey(key) ? dict[key] : default(TValue);

        public static void AddIfNotNull<T>(this List<T> list, T item)
        {
            if (!EqualityComparer<T>.Default.Equals(item, default(T)))
                list.Add(item);
        }

        public static MD5Hash GetMD5Hash(this DbDataReader dataReader, int ordinal)
        {
            byte[] buffer = new byte[MD5Hash.ExpectedByteLength];
            if (dataReader.GetBytes(ordinal, 0, buffer, 0, buffer.Length) < MD5Hash.ExpectedByteLength)
                throw new ArgumentException("Failed to read the correct number of bytes for a MD5Hash");

            return new MD5Hash(buffer);
        }

        /// <summary>
        /// Where an object is stored in the DB - deserialize
        /// </summary>
        /// <typeparam name="T">The class type to deserialize</typeparam>
        /// <param name="dataReader"></param>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        public static T DeserializeField<T>(this DbDataReader dataReader, int ordinal) where T : class
        {
            if (dataReader.IsDBNull(ordinal)) return null;

            byte[] buffer = (byte[])dataReader.GetValue(ordinal);

            using (var ms = new MemoryStream(buffer))
            {
                var o = formatter.Deserialize(ms) as T;
                if (o == null) throw new FetchoException(string.Format("Deserialization to {0} failed", typeof(T)));
                return o;
            }
        }
        static readonly BinaryFormatter formatter = new BinaryFormatter();

        public static void SetBinaryParameter(this NpgsqlCommand cmd, string parameterName, object value)
        {
            if (value != null)
                using (var ms = new MemoryStream(1000))
                {
                    formatter.Serialize(ms, value);
                    cmd.Parameters.AddWithValue(parameterName, ms.GetBuffer());
                }
            else
                cmd.Parameters.AddWithValue(parameterName, DBNull.Value);

        }




    }


}
