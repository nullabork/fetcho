
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Fetcho.Common
{
    public enum WebDataPacketReaderSection
    {
        Request, Response, Exception, None
    };

    public class WebDataPacketReader : IDisposable
    {

        /// <summary>
        /// Are we at the end of the stream
        /// </summary>
        public bool EndOfFile { get { return inStream.EOF; } }

        public WebDataPacketReaderSection CurrentSection { get; private set; }

        /// <summary>
        /// Raw stream we're accessing
        /// </summary>
        private readonly XmlReader inStream;

        /// <summary>
        /// A forward only packet reader
        /// </summary>
        /// <param name="inStream"></param>
        public WebDataPacketReader(Stream inStream) : this(XmlReader.Create(inStream))
        {

        }

        public WebDataPacketReader(XmlReader reader)
        {
            this.inStream = reader;
            if (reader.NodeType == XmlNodeType.None)
                if (!NextResource())
                    throw new Exception("No resources in the file");
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
            ReadToElement("header");
            if (inStream.Name != "header") return String.Empty;

            return inStream.ReadElementContentAsString();
        }

        /// <summary>
        /// Returns a stream for the current file
        /// </summary>
        /// <returns></returns>
        public Stream GetResponseStream()
        {
            ReadToElement("data");
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

        private void ReadToElement(string name)
        {
            if (inStream.NodeType == XmlNodeType.EndElement && inStream.Name == "resource") return;
            while (inStream.Read() && !(inStream.NodeType == XmlNodeType.Element && inStream.Name == name))
                if (inStream.NodeType == XmlNodeType.EndElement && inStream.Name == "resource")
                    break;
        }

        public static Uri GetUriFromRequestString(string requestString)
        {
            if (!requestString.StartsWith("Uri:")) return null;

            int i = requestString.IndexOf("\n");
            if (i < 0) return null;

            string uri = requestString.Substring(4, i - 4).Trim();

            return new Uri(uri);
        }
    }
}
