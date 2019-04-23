using Fetcho.Common;
using Fetcho.ContentReaders;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Fetcho
{
    public class ExtractKeywordsConsumer : WebDataPacketConsumer
    {
        private FastLookupCache<string> cache = new FastLookupCache<string>(10000);

        public Uri CurrentUri;
        public ContentType ContentType;

        public override string Name { get => "Extract Keywords"; }
        public override bool ProcessesRequest { get => true; }
        public override bool ProcessesResponse { get => true; }

        public ExtractKeywordsConsumer() { }

        public override async Task ProcessRequest(string request) =>
            CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);

        public override async Task ProcessResponseHeaders(string responseHeaders) =>
            ContentType = WebDataPacketReader.GetContentTypeFromResponseHeaders(responseHeaders);

        public override async Task ProcessResponseStream(Stream dataStream)
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

        public override void NewResource()
        {
            CurrentUri = null;
            ContentType = null;
        }

        private bool writtenUri = false;
        private void WriteExtractedText(BracketPipeTextFragment fragment)
        {
            string value = fragment.Text;
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
