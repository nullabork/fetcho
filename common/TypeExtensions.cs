using System;
using System.Text;
using System.Xml;

namespace Fetcho.Common
{
    /// <summary>
    /// Extension methods for various types
    /// </summary>
    public static class TypeExtensions
    {
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

        /// <summary>
        /// Ensure a string always only comes to maxlength
        /// </summary>
        /// <param name="str"></param>
        /// <param name="maxlength"></param>
        /// <returns></returns>
        public static string Truncate(this string str, int maxlength)
        {
            if (str.Length < maxlength) return str;
            return str.Substring(0, maxlength);
        }

        /// <summary>
        /// Strip non-XML chars from the provided string
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string CleanupForXml(this string str)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < str.Length; i++)
                if (XmlConvert.IsXmlChar(str[i]))
                    sb.Append(str[i]);

            return sb.ToString();
        }
    }

    
}
