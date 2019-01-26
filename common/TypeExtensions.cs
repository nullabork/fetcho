using System;

namespace Fetcho.Common
{
    /// <summary>
    /// Description of TypeExtensions.
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

        public static T[] PadLeft<T>(this T[] array1, T array2, int number)
        {
            if (array1.Length >= number)
                return array1;

            T[] c = new T[number];
            Array.Copy(array1, 0, c, number - array1.Length, array1.Length);
            for (int i = 0; i < number - array1.Length; i++)
                c[i] = array2;

            return c;
        }

        public static T[] PadRight<T>(this T[] array1, T array2, int number)
        {
            if (array1.Length >= number)
                return array1;

            T[] c = new T[number];
            Array.Copy(array1, 0, c, 0, array1.Length);
            for (int i = 0; i < number - array1.Length; i++)
                c[array1.Length + i] = array2;

            return c;
        }
    }
}
