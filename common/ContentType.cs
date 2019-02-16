using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Fetcho.Common
{
    public class ContentType : IEqualityComparer<ContentType>, IComparer<ContentType>
    {
        public string Raw { get; private set; }

        public string MediaType { get; private set; }

        public string SubType { get; private set; }

        public KeyValuePair<string, string>[] Attributes { get; private set; }

        public bool IsBlank { get => String.IsNullOrWhiteSpace(Raw); }

        public ContentType(string contentType)
        {
            if (String.IsNullOrWhiteSpace(contentType)) contentType = String.Empty;
            Raw = contentType;
            Parse(contentType);
        }

        private ContentType() { }

        private void Parse(string contentType)
        {
            string[] attrs = contentType.Split(';');

            int index = attrs[0].IndexOf('/');
            if (index < 0)
                MediaType = attrs[0];
            else
            {
                MediaType = attrs[0].Substring(0, index);
                SubType = attrs[0].Substring(index + 1);
            }

            var l = new List<KeyValuePair<string, string>>();
            for (int i = 1; i < attrs.Length; i++)
            {
                string[] parts = attrs[i].Split('=');
                if (parts.Length != 2) continue;
                l.Add(new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim()));
            }

            Attributes = l.ToArray();
        }

        private string GetAttributesString()
        {
            var sb = new StringBuilder();

            foreach (var kvp in Attributes)
            {
                sb.Append(kvp.Key);
                sb.Append("=");
                sb.Append(kvp.Value);
                sb.Append("; ");
            }

            if (sb.Length > 0) sb.Remove(sb.Length - 2, 2);

            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj as ContentType == null) return false;
            return this.Equals(this, obj as ContentType);
        }

        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        public bool Equals(ContentType x, ContentType y)
        {
            return x.Raw == y.Raw;
        }

        public int GetHashCode(ContentType obj)
        {
            return Raw.GetHashCode();
        }

        public int Compare(ContentType x, ContentType y)
        {
            return x.Raw.CompareTo(y);
        }

        public static bool operator ==(ContentType lhs, ContentType rhs) => lhs.Equals(rhs);

        public static bool operator !=(ContentType lhs, ContentType rhs) => !lhs.Equals(rhs);

        public override string ToString()
        {
            if (String.IsNullOrWhiteSpace(Raw)) return String.Empty;

            string attr = GetAttributesString();
            if (String.IsNullOrWhiteSpace(attr))
                return string.Format("{0}/{1}", MediaType, SubType);
            else
                return string.Format("{0}/{1}; {2}", MediaType, SubType, attr);
        }

        public static readonly ContentType Unknown = new ContentType(String.Empty);

        public static bool IsUnknownOrNull(ContentType contentType) => 
            contentType == null || 
            contentType == ContentType.Unknown ||
            contentType.IsBlank;

        [DllImport(@"urlmon.dll",
                       CharSet = CharSet.Unicode,
                       ExactSpelling = true,
                       SetLastError = false)]
        private extern static int FindMimeFromData(
              IntPtr pBC,
              [MarshalAs(UnmanagedType.LPWStr)] string pwzUrl,
              [MarshalAs(UnmanagedType.LPArray,
                 ArraySubType=UnmanagedType.I1,
                 SizeParamIndex=3)]
              byte[] pBuffer,
              int cbSize,
              [MarshalAs(UnmanagedType.LPWStr)] string pwzMimeProposed,
              int dwMimeFlags,
              out System.IntPtr ppwzMimeOut,
              int dwReserverd
             );

        public const int BytesRequiredForGuessing = 256;

        public static ContentType Guess(string filename)
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException(filename + " not found");

            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                return Guess(fs);
            }
        }

        public static ContentType Guess(Stream stream)
        {
            byte[] buffer = new byte[BytesRequiredForGuessing];
            int bytesRead = stream.Read(buffer, 0, BytesRequiredForGuessing);
            return Guess(buffer);
        }

        public static ContentType Guess(byte[] bytes)
        {
            //if (!Environment.Is64BitProcess)
            //    throw new Exception("DetectContentTypeFromBytes(): This is not a 64 bit process. This method isnt compatible with 32bit systems.");

            if (bytes == null) return ContentType.Unknown;

            byte[] buffer = null;

            if (bytes.Length > BytesRequiredForGuessing)
            {
                buffer = new byte[BytesRequiredForGuessing];
                Array.Copy(bytes, buffer, BytesRequiredForGuessing);
            }
            else
                buffer = bytes;

            System.IntPtr mimetypePtr = IntPtr.Zero;
            string mime = String.Empty;

            try
            {
                FindMimeFromData(IntPtr.Zero, null, buffer, buffer.Length, null, 0, out mimetypePtr, 0);
                mime = Marshal.PtrToStringUni(mimetypePtr);
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
                mime = String.Empty;
            }
            finally
            {
                Marshal.FreeCoTaskMem(mimetypePtr);
            }

            return new ContentType(mime);
        }
    }


}
