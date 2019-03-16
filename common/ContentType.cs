using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HeyRed.Mime;

namespace Fetcho.Common
{
    public class ContentType : IEqualityComparer<ContentType>, IComparer<ContentType>
    {
        public const string ApplicationJson = "application/json";
        public const string ApplicationXEmpty = "application/x-empty";

        public string Raw { get; private set; }

        public string MediaType { get; private set; }

        public string SubType { get; private set; }

        public KeyValuePair<string, string>[] Attributes { get; private set; }

        public bool IsBlank { get => String.IsNullOrWhiteSpace(Raw); }
        public bool IsTextType { get => ContentType.IsTextContentType(this); }
        public bool IsXmlType { get => ContentType.IsXmlContentType(this); }
        public bool IsBinaryType { get => ContentType.IsBinaryContentType(this); }
        public bool IsHtmlType { get => ContentType.IsHtmlContentType(this); }

        public ContentType(string contentType)
        {
            if (String.IsNullOrWhiteSpace(contentType)) contentType = String.Empty;
            Raw = contentType;
            Parse(contentType);
        }

        private ContentType() { }

        private void Parse(string contentType)
        {
            if (contentType == String.Empty) return;
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
            => GetHashCode(this);

        public bool Equals(ContentType x, ContentType y)
            => x.Raw == y.Raw;

        public int GetHashCode(ContentType obj)
            => Raw.GetHashCode();

        public int Compare(ContentType x, ContentType y)
            => x.Raw.CompareTo(y);

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

        public static readonly ContentType Empty = new ContentType(ApplicationXEmpty);

        public static bool IsUnknownOrNull(ContentType contentType) => 
            contentType == null || 
            contentType == ContentType.Unknown ||
            contentType.IsBlank;

        public const int BytesRequiredForGuessing = 256;

        public static bool IsXmlContentType(ContentType value) 
            => value.MediaType == "application" && value.SubType.Contains("xml");

        public static bool IsTextContentType(ContentType value) 
            => value.MediaType == "text";

        public static bool IsBinaryContentType(ContentType value)
            => value.MediaType == "binary";

        public static bool IsHtmlContentType(ContentType value) 
            => value.MediaType == "text" && value.SubType == "html";

        public static ContentType Guess(string fileName)
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException(fileName + " not found");

            using (FileStream fs = new FileStream(fileName, FileMode.Open))
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

        public static ContentType Guess(byte[] value)
        {
            if (value == null) return ContentType.Unknown;

            return new ContentType(MimeGuesser.GuessMimeType(value));
        }
    }


}
