using Fetcho.Common;
using Fetcho.ContentReaders;
using System;
using System.IO;

namespace Fetcho
{
    internal class ExtractKeywordsConsumer : IWebDataPacketConsumer
    {
        public Uri CurrentUri;
        public ContentType ContentType;

        public string Name { get => "Extract Keywords"; }
        public bool ProcessesRequest { get => true; }
        public bool ProcessesResponse { get => true; }
        public bool ProcessesException { get => false; }

        public void ProcessException(string exception)
        {
        }

        public void ProcessRequest(string request)
        {
            CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);
        }

        public void ProcessResponseHeaders(string responseHeaders)
        {
            ContentType = WebDataPacketReader.GetContentTypeFromResponseHeaders(responseHeaders);
        }

        public void ProcessResponseStream(Stream dataStream)
        {
            if ( ContentType.MediaType == "text" && ContentType.SubType == "html")
            {
                writtenUri = false;
                var parser = new HTMLKeywordExtractor
                {
                    MinimumLength = 256,
                    IncludeChardata = false
                };
                parser.Parse(dataStream, WriteKeyword);
            }

        }

        public void NewResource()
        {
            CurrentUri = null;
            ContentType = null;
        }

        public void PacketClosed() { }

        public void PacketOpened() { }

        public void ReadingException(Exception ex) { }

        private bool writtenUri = false;
        private void WriteKeyword(string keyword)
        {
            if ( !writtenUri)
            {
                writtenUri = true;
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine(CurrentUri);
            }
            Console.Write("{0} ", keyword);
        }
    }

}
