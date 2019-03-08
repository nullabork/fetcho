
using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace Fetcho.Common
{

    public class WebDataPacketReader : IDisposable
    {

        /// <summary>
        /// Raw stream we're accessing
        /// </summary>
        private readonly XmlReader inStream;

        private string currentException = "";

        /// <summary>
        /// A forward only packet reader
        /// </summary>
        /// <param name="inStream"></param>
        public WebDataPacketReader(Stream inStream) : this(CreateDefaultXmlReader(inStream))
        {

        }

        public WebDataPacketReader(XmlReader reader)
        {
            this.inStream = reader;
            if (reader.NodeType == XmlNodeType.None)
                if (!NextResource())
                    throw new FetchoException("No resources in the file");
        }

        public WebDataPacketReader(string fileName) : this(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {

        }

        /// <summary>
        /// Returns a stream for the current request
        /// </summary>
        /// <returns></returns>
        public string GetRequestString()
        {
            ReadToElement("request");

            if (inStream.Name != "request")
                return String.Empty;

            return inStream.ReadElementContentAsString();
        }

        public string GetResponseHeaders()
        {
            ReadToElement("header", "exception");
            if (inStream.Name == "exception") 
            {
                ReadExceptionIntoCache();
                ReadToElement("header");
            }

            if (inStream.Name != "header") return String.Empty;

            return inStream.ReadElementContentAsString();
        }

        /// <summary>
        /// Returns a stream for the current file
        /// </summary>
        /// <returns></returns>
        public Stream GetResponseStream()
        {
            ReadToElement("data", "exception");
            if (inStream.Name == "exception")
            {
                ReadExceptionIntoCache();
                ReadToElement("data");
            }

            if (inStream.Name == "data")
                return new XmlBase64ElementStream(inStream);
            return null;
        }

        /// <summary>
        /// Returns a string of the exception if any
        /// </summary>
        /// <returns>A string of all the exception text</returns>
        public string GetException()
        {
            if (!String.IsNullOrWhiteSpace(this.currentException))
            {
                string ex = currentException;
                currentException = String.Empty;
                return ex;
            }

            ReadToElement("exception");
            if (inStream.Name == "exception")
                return inStream.ReadElementContentAsString();
            else
                return String.Empty;
        }

        /// <summary>
        /// Skip to the next file
        /// </summary>
        /// <returns></returns>
        public bool NextResource()
        {
            while (inStream.Read() && !(inStream.Name == "resource" && inStream.NodeType == XmlNodeType.Element)) ;

            return !inStream.EOF;
        }

        public void Dispose()
        {
            inStream?.Dispose();
        }

        private void ReadExceptionIntoCache()
        {
            if (inStream.Name == "exception")
                currentException = inStream.ReadElementContentAsString();
            else
                currentException = String.Empty;
        }

        private void ReadToElement(params string [] names)
        {
            if (inStream.NodeType == XmlNodeType.Element && names.Contains(inStream.Name)) return;
            if (inStream.NodeType == XmlNodeType.EndElement && inStream.Name == "resource") return;
            while (inStream.Read() && !(inStream.NodeType == XmlNodeType.Element && names.Contains(inStream.Name)))
                if (inStream.NodeType == XmlNodeType.EndElement && inStream.Name == "resource")
                    break;
        }

        private static XmlReader CreateDefaultXmlReader(Stream inStream)
        {
            var settings = new XmlReaderSettings();
            settings.ConformanceLevel = ConformanceLevel.Fragment;
            return XmlReader.Create(inStream, settings);
        }

        /// <summary>
        /// Extract the URI from the raw request string
        /// </summary>
        /// <param name="requestString"></param>
        /// <returns></returns>
        public static Uri GetUriFromRequestString(string requestString)
        {
            const string UriPrefix = "Uri:";

            if (String.IsNullOrWhiteSpace(requestString)) return null;
            if (!requestString.StartsWith(UriPrefix)) return null;

            int i = requestString.IndexOf("\n", StringComparison.InvariantCultureIgnoreCase);
            if (i < 0) return null;

            string uri = requestString.Substring(UriPrefix.Length, i - UriPrefix.Length).Trim();

            return new Uri(uri);
        }
        
        /// <summary>
        /// Extract the Referrer URI from the raw request string
        /// </summary>
        /// <param name="requestString"></param>
        /// <returns></returns>
        public static Uri GetReferrerUriFromRequestString(string requestString)
        {
            const string ReferrerPrefix = "Referrer:";

            if (String.IsNullOrWhiteSpace(requestString)) return null;
            if (requestString.IndexOf(ReferrerPrefix, StringComparison.InvariantCultureIgnoreCase) < 0) return null;

            requestString = requestString.Substring(requestString.IndexOf(ReferrerPrefix, StringComparison.InvariantCultureIgnoreCase));
            int i = requestString.IndexOf("\n", StringComparison.InvariantCultureIgnoreCase);
            if (i < 0) return null;

            string uri = requestString.Substring(ReferrerPrefix.Length, i - ReferrerPrefix.Length).Trim();

            return new Uri(uri);
        }

        public static ContentType GetContentTypeFromResponseHeaders( string responseHeaders )
        {
            const string ContentTypePrefix = "content-type:";

            string contentType = String.Empty;

            int index = responseHeaders.IndexOf(ContentTypePrefix, StringComparison.InvariantCultureIgnoreCase);
            if ( index >= 0 )
            {
                int endIndex = responseHeaders.IndexOf("\n", index);
                if (endIndex > 0)
                    contentType = responseHeaders.Substring(index + ContentTypePrefix.Length, endIndex - index - ContentTypePrefix.Length);
                else
                    contentType = responseHeaders.Substring(index + ContentTypePrefix.Length);
            }

            return new ContentType(contentType.Trim());
        }

        /// <summary>
        /// Determines if the string passed represents an Exception
        /// </summary>
        /// <param name="exceptionString"></param>
        /// <returns></returns>
        public static bool IsException(string exceptionString) => !String.IsNullOrWhiteSpace(exceptionString);
    }
}
