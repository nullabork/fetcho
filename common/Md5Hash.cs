
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;


namespace Fetcho.Common
{
  public class MD5Hash : IComparable<MD5Hash>, IComparable
  {
    public const int ExpectedByteLength = 16;
    public const int ExpectedStringLength = ExpectedByteLength * 2;
    
    public byte[] Values { get; protected set; }
    
    
    public MD5Hash( object obj ) : this(obj.ToString())
    {
      
    }
    
    public MD5Hash( string hashString )
    {
      if ( hashString.Length != ExpectedStringLength )
        throw new ArgumentException("Unexpected string length: " + hashString.Length + ". Expected " + ExpectedStringLength,
                                    "hashString");
      
      Values = GetBytesFromHash(hashString);
    }
    
    public MD5Hash( byte[] copyValues )
    {
      if ( copyValues.Length != ExpectedByteLength )
        throw new ArgumentException("Unexpected byte length: " + copyValues.Length + ". Expected " + ExpectedByteLength,
                                    "values");
      Values = new Byte[ExpectedByteLength];
      Array.Copy(copyValues, Values, ExpectedByteLength);
    }
    
    public override string ToString()
    {
      var sb = new StringBuilder();
      foreach( byte b in Values )
        sb.AppendFormat("{0:X2}", b);
      return sb.ToString();
    }
    
    #region Equals and GetHashCode implementation
    public override bool Equals(object obj)
    {
      MD5Hash other = obj as MD5Hash;
      if (other == null)
        return false;
      if ( this.Values.Length != other.Values.Length )
        return false;
      
      return Values.SequenceEqual(other.Values);
    }
    
    public override int GetHashCode()
    {
      int hashCode = 0;
      unchecked {
        if (Values != null)
          hashCode += 1000000007 * Values.GetHashCode();
      }
      return hashCode;
    }
    
    public static bool operator ==(MD5Hash lhs, MD5Hash rhs)
    {
      if (ReferenceEquals(lhs, rhs))
        return true;
      if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null))
        return false;
      return lhs.Equals(rhs);
    }
    
    public static bool operator !=(MD5Hash lhs, MD5Hash rhs)
    {
      return !(lhs == rhs);
    }
    #endregion
    
    public static bool operator <= (MD5Hash lhs, MD5Hash rhs )
    {
      return HashRange.GetBigIntegerFromBytes(lhs.Values) <= HashRange.GetBigIntegerFromBytes(rhs.Values);
    }
    
    public static bool operator > (MD5Hash lhs, MD5Hash rhs )
    {
      return !(lhs <= rhs);
    }
    
    public static bool operator >= (MD5Hash lhs, MD5Hash rhs )
    {
      return HashRange.GetBigIntegerFromBytes(lhs.Values) >= HashRange.GetBigIntegerFromBytes(rhs.Values);
    }
    
    public static bool operator < (MD5Hash lhs, MD5Hash rhs )
    {
      return !(lhs >= rhs);
    }
    
    
    public readonly static MD5Hash MinValue = new MD5Hash( new byte[] {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0});
    public readonly static MD5Hash MaxValue = new MD5Hash( new byte[] {255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255});
    
    public static MD5Hash Compute( byte[] bytes )
    {
      if ( bytes == null ) bytes = new byte[0];
      return new MD5Hash(ComputeMd5Hash(bytes));
    }
    
    public static MD5Hash Compute( object o )
    {
      return new MD5Hash(ComputeMd5Hash(o));
    }
    
    public static MD5Hash Compute( Stream stream )
    {
      return new MD5Hash(ComputeMd5Hash(stream));
    }
    
    public static bool IsValid( MD5Hash hash, Stream stream )
    {
      return hash.Equals(ComputeMd5Hash(stream));
    }
    
    public static MD5Hash Parse( string hashString )
    {
      if ( hashString.Length != ExpectedStringLength )
        throw new ArgumentException("Not expected length of " + ExpectedStringLength, "hashString");
      
      return new MD5Hash( GetBytesFromHash(hashString));
    }
    
    public static bool TryParse( string potentialHash, out MD5Hash hash )
    {
      bool rtn = false;
      
      try {
        hash = Parse(potentialHash);
        rtn = true;
      }
      catch( Exception )
      {
        hash = null;
        rtn = false;
      }
      
      return rtn;
    }
    
    private static byte[] ComputeMd5Hash( byte[] bytes )
    {
      using (HashAlgorithm algorithm = MD5.Create())  //or use SHA1.Create();
      {
        return algorithm.ComputeHash(bytes);
      }
    }
    
    private static byte[] ComputeMd5Hash( object o )
    {
      using (HashAlgorithm algorithm = MD5.Create())  //or use SHA1.Create();
      {
        return algorithm.ComputeHash(Encoding.UTF8.GetBytes(o.ToString()));
      }
    }
    
    private static byte[] ComputeMd5Hash( Stream stream )
    {
      using (HashAlgorithm algorithm = MD5.Create())  //or use SHA1.Create();
      {
        return algorithm.ComputeHash(stream);
      }
    }
    
    private static byte[] GetBytesFromHash( string hashString )
    {
      byte[] bytes = new byte[hashString.Length/2];
      for ( int i=0;i<hashString.Length;i+=2)
        bytes[i/2] = Convert.ToByte(hashString.Substring(i,2), 16);
      return bytes;
    }
    
    
    public int CompareTo(object obj)
    {
      MD5Hash hash = obj as MD5Hash;
      return CompareTo(hash);
    }
    
    public int CompareTo(MD5Hash other)
    {
      if ( other == null ) return -1;
      if ( other < this ) return -1;
      else if ( other > this ) return 1;
      else return 0;
    }
  }
}
