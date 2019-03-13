﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;

namespace Fetcho.Common
{
    /// <summary>
    /// Extension methods for various types
    /// </summary>
    public static class TypeExtensions
    {
        static Random random = new Random(DateTime.Now.Millisecond);

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

        public static int MaxConstraint(this int val, int maxValue) => val > maxValue ? maxValue : val;  
        public static int MinConstraint(this int val, int minValue) => val < minValue ? minValue : val;
        public static int RangeConstraint(this int val, int min, int max) => val < min ? min : val > max ? max : val;

        public static long MaxConstraint(this long val, long max) => val > max ? max : val;
        public static long MinConstraint(this long val, long min) => val < min ? min : val;
        public static long RangeConstraint(this long val, long min, long max) => val < min ? min : val > max ? max : val;

        public static double MaxConstraint(this double val, double max) => val > max ? max : val;
        public static double MinConstraint(this double val, double min) => val < min ? min : val;
        public static double RangeConstraint(this double val, double min, double max) => val < min ? min : val > max ? max : val;

        public static decimal MaxConstraint(this decimal val, decimal max) => val > max ? max : val;
        public static decimal MinConstraint(this decimal val, decimal min) => val < min ? min : val;
        public static decimal RangeConstraint(this decimal val, decimal min, decimal max) => val < min ? min : val > max ? max : val;

        public static bool Between(this int val, int min, int max) => val >= min && val <= max;
        public static bool Between(this long val, long min, long max) => val >= min && val <= max;
        public static bool Between(this decimal val, decimal min, decimal max) => val >= min && val <= max;
        public static bool Between(this double val, double min, double max) => val >= min && val <= max;

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
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < value.Length; i++)
                if (XmlConvert.IsXmlChar(value[i]))
                    sb.Append(value[i]);

            return sb.ToString();
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
    }

    
}
