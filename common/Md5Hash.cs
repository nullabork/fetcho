
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;


namespace Fetcho.Common
{
    /// <summary>
    /// Class representing a MD5 Hash
    /// </summary>
    public class MD5Hash : IComparable<MD5Hash>, IComparable
    {
        public const int ExpectedByteLength = 16;
        public const int ExpectedStringLength = ExpectedByteLength * 2;

        /// <summary>
        /// byte values for the hash
        /// </summary>
        public byte[] Values { get; protected set; }

        /// <summary>
        /// Create a MD5 hash object from an object that converts to a string representation of a hash
        /// </summary>
        /// <param name="obj"></param>
        public MD5Hash(object obj) : this(obj.ToString()) {}

        /// <summary>
        /// Create a MD5 hash object from its string representation
        /// </summary>
        /// <param name="hashString"></param>
        public MD5Hash(string hashString)
        {
            if (hashString.Length != ExpectedStringLength)
                throw new ArgumentException("Unexpected string length: " + hashString.Length + ". Expected " + ExpectedStringLength,
                                            "hashString");

            Values = GetBytesFromHash(hashString);
        }

        /// <summary>
        /// Copy an existing hash and create a new one
        /// </summary>
        /// <param name="copyValues"></param>
        public MD5Hash(byte[] copyValues)
        {
            if (copyValues.Length != ExpectedByteLength)
                throw new ArgumentException("Unexpected byte length: " + copyValues.Length + ". Expected " + ExpectedByteLength,
                                            "values");
            Values = new byte[ExpectedByteLength];
            Array.Copy(copyValues, Values, ExpectedByteLength);
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="copy"></param>
        public MD5Hash(MD5Hash copy) : this(copy?.Values) { }

        /// <summary>
        /// Cache of the hash string
        /// </summary>
        private string toStringCache = string.Empty;

        public override string ToString()
        {
            if (toStringCache.Length == 0)
            {
                var sb = new StringBuilder();
                foreach (byte b in Values)
                    sb.AppendFormat("{0:x2}", b);
                toStringCache = sb.ToString();
            }
            return toStringCache;
        }

        #region Equals and GetHashCode implementation
        public override bool Equals(object obj)
        {
            MD5Hash other = obj as MD5Hash;
            if (other == null)
                return false;
            if (this.Values.Length != other.Values.Length)
                return false;

            return Values.SequenceEqual(other.Values);
        }

        public override int GetHashCode()
        {
            int hashCode = 0;
            unchecked
            {
                if (Values != null)
                    hashCode += 1000000007 * Values.GetHashCode();
            }
            return hashCode;
        }

        public static bool operator ==(MD5Hash lhs, MD5Hash rhs)
        {
            if (ReferenceEquals(lhs, rhs))
                return true;
            return ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null) ? false : lhs.Equals(rhs);
        }

        public static bool operator !=(MD5Hash lhs, MD5Hash rhs)
        {
            return !(lhs == rhs);
        }
        #endregion

        public static bool operator <=(MD5Hash lhs, MD5Hash rhs)
        {
            return HashRange.GetBigIntegerFromBytes(lhs.Values) <= HashRange.GetBigIntegerFromBytes(rhs.Values);
        }

        public static bool operator >(MD5Hash lhs, MD5Hash rhs)
        {
            return !(lhs <= rhs);
        }

        public static bool operator >=(MD5Hash lhs, MD5Hash rhs)
        {
            return HashRange.GetBigIntegerFromBytes(lhs.Values) >= HashRange.GetBigIntegerFromBytes(rhs.Values);
        }

        public static bool operator <(MD5Hash lhs, MD5Hash rhs)
        {
            return !(lhs >= rhs);
        }

        /// <summary>
        /// MinValue of a hash
        /// </summary>
        public readonly static MD5Hash MinValue = new MD5Hash(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

        /// <summary>
        /// Max value for a hash
        /// </summary>
        public readonly static MD5Hash MaxValue = new MD5Hash(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 });

        /// <summary>
        /// Empty hash
        /// </summary>
        public readonly static MD5Hash Empty = new MD5Hash(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

        /// <summary>
        /// Create a hash of the bytes supplied
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static MD5Hash Compute(byte[] bytes)
        {
            if (bytes == null) bytes = new byte[0];
            return new MD5Hash(ComputeMd5Hash(bytes));
        }

        /// <summary>
        /// Compute a hash from the supplied object
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static MD5Hash Compute(object o)
        {
            return new MD5Hash(ComputeMd5Hash(o));
        }

        /// <summary>
        /// Compute a hash from the supplied data stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static MD5Hash Compute(Stream stream)
        {
            return new MD5Hash(ComputeMd5Hash(stream));
        }

        /// <summary>
        /// Determines if a streamhash equals the hash supplied
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static bool IsValid(MD5Hash hash, Stream stream)
        {
            return hash.Equals(ComputeMd5Hash(stream));
        }

        /// <summary>
        /// Parse a textual hash representation into a hash object
        /// </summary>
        /// <param name="hashString"></param>
        /// <returns></returns>
        public static MD5Hash Parse(string hashString)
        {
            if (hashString.Length != ExpectedStringLength)
                throw new ArgumentException("Not expected length of " + ExpectedStringLength, "hashString");

            return new MD5Hash(GetBytesFromHash(hashString));
        }

        /// <summary>
        /// Try and parse a potential hash into a hash object
        /// </summary>
        /// <param name="potentialHash"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        public static bool TryParse(string potentialHash, out MD5Hash hash)
        {
            bool rtn = false;

            try
            {
                hash = Parse(potentialHash);
                rtn = true;
            }
            catch (Exception)
            {
                hash = null;
                rtn = false;
            }

            return rtn;
        }

        /// <summary>
        /// Computer a MD5 hash from some bytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static byte[] ComputeMd5Hash(byte[] bytes)
        {
            using (HashAlgorithm algorithm = MD5.Create())  //or use SHA1.Create();
            {
                return algorithm.ComputeHash(bytes);
            }
        }

        /// <summary>
        /// Compute a MD5 hash from an object
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        private static byte[] ComputeMd5Hash(object o)
        {
            using (HashAlgorithm algorithm = MD5.Create())  //or use SHA1.Create();
            {
                return algorithm.ComputeHash(Encoding.UTF8.GetBytes(o.ToString()));
            }
        }

        /// <summary>
        /// Compute a MD5 hash from a stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        private static byte[] ComputeMd5Hash(Stream stream)
        {
            using (HashAlgorithm algorithm = MD5.Create())  //or use SHA1.Create();
            {
                return algorithm.ComputeHash(stream);
            }
        }

        /// <summary>
        /// Convert a hash string into its byte representation
        /// </summary>
        /// <param name="hashString"></param>
        /// <returns></returns>
        private static byte[] GetBytesFromHash(string hashString)
        {
            byte[] bytes = new byte[hashString.Length / 2];
            for (int i = 0; i < hashString.Length; i += 2)
                bytes[i / 2] = Convert.ToByte(hashString.Substring(i, 2), 16);
            return bytes;
        }

        /// <summary>
        /// Compare this hash to another hash
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(object other)
        {
            MD5Hash hash = other as MD5Hash;
            return CompareTo(hash);
        }

        /// <summary>
        /// Compare this hash to another hash
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(MD5Hash other)
        {
            if (other == null) return -1;
            if (other < this) return -1;
            else if (other > this) return 1;
            else return 0;
        }
    }
}
