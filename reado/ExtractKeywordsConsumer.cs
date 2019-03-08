using Fetcho.Common;
using Fetcho.ContentReaders;
using System;
using System.IO;

namespace Fetcho
{
    internal class ExtractKeywordsConsumer : IWebDataPacketConsumer
    {
        private FastLookupCache<string> cache = new FastLookupCache<string>(10000);

        public Uri CurrentUri;
        public ContentType ContentType;

        public string Name { get => "Extract Keywords"; }
        public bool ProcessesRequest { get => true; }
        public bool ProcessesResponse { get => true; }
        public bool ProcessesException { get => false; }

        public ExtractKeywordsConsumer() { }

        public void ProcessException(string exception) { }

        public void ProcessRequest(string request) =>
            CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);

        public void ProcessResponseHeaders(string responseHeaders) =>
            ContentType = WebDataPacketReader.GetContentTypeFromResponseHeaders(responseHeaders);

        public void ProcessResponseStream(Stream dataStream)
        {
            if (ContentType.IsTextType || ContentType.IsXmlType)
            {
                writtenUri = false;
                var parser = new BracketPipeTextExtractor
                {
                    Distinct = true,
                    Granularity = ExtractionGranularity.Raw,
                    MaximumLength = int.MaxValue,
                    MinimumLength = int.MinValue,
                    StopWords = false
                };
                parser.Parse(dataStream, WriteExtractedText);
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
        private void WriteExtractedText(string value)
        {
            value = value.Trim();
            if (cache.Contains(value)) return;
            cache.Enqueue(value);

            if (!writtenUri)
            {
                writtenUri = true;
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine(CurrentUri);
            }
            Console.Write("{0} ", value);
        }
    }

}
