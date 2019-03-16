using Fetcho.Common;
using Fetcho.ContentReaders;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks.Dataflow;

namespace Fetcho
{
    internal class ExtractLinksAndBufferConsumer : WebDataPacketConsumer
    {
        static readonly ILog log = LogManager.GetLogger(typeof(ExtractLinksAndBufferConsumer));

        public Uri CurrentUri;
        public ContentType ContentType;
        public BufferBlock<IEnumerable<QueueItem>> PrioritisationBuffer;
        public int LinksExtracted;

        public ExtractLinksAndBufferConsumer(BufferBlock<IEnumerable<QueueItem>> prioritisationBuffer)
        {
            PrioritisationBuffer = prioritisationBuffer;
        }

        public override string Name { get => "Extract Links"; }
        public override bool ProcessesRequest { get => true; }
        public override bool ProcessesResponse { get => true; }

        public override void ProcessRequest(string request)
            => CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);

        public override void ProcessResponseHeaders(string responseHeaders)
            => ContentType = WebDataPacketReader.GetContentTypeFromResponseHeaders(responseHeaders);

        public override void ProcessResponseStream(Stream dataStream)
        {
            if (dataStream == null) return;
            var extractor = GuessLinkExtractor(dataStream);
            if (extractor != null)
            {
                SendUris(extractor);
                extractor.Dispose();
                extractor = null;
            }
        }

        public override void NewResource()
        {
            CurrentUri = null;
            ContentType = null;
        }

        private ILinkExtractor GuessLinkExtractor(Stream dataStream)
        {
            var ms = new MemoryStream();
            dataStream.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);

            if (ContentType.IsUnknownOrNull(ContentType))
                ContentType = ContentType.Guess(ms);

            ms.Seek(0, SeekOrigin.Begin);

            if ( ContentType.IsHtmlContentType(ContentType))
                return new HtmlFileLinkExtractor(CurrentUri, ms);
            else if (ContentType.IsXmlContentType(ContentType))
                return new TextFileLinkExtractor(CurrentUri, new StreamReader(ms));
            else if (ContentType.IsTextContentType(ContentType))
                return new TextFileLinkExtractor(CurrentUri, new StreamReader(ms));
            else if(ContentType.IsUnknownOrNull(ContentType))
                return new TextFileLinkExtractor(CurrentUri, new StreamReader(ms));
            else
            {
                ms.Dispose();
                ms = null;
                log.InfoFormat("No link extractor for content type: {0}, from {1}", ContentType, CurrentUri);
                return null;
            }
        }

        private void SendUris(ILinkExtractor reader)
        {
            var l = new List<QueueItem>();
            if (reader == null) return;

            Uri uri = reader.NextUri();

            while (uri != null)
            {
                var item = new QueueItem()
                {
                    SourceUri = reader.CurrentSourceUri,
                    TargetUri = uri
                };

                l.Add(item);
                uri = reader.NextUri();
            }

            LinksExtracted += l.Count;
            // effectively block until the URL is accepted
            PrioritisationBuffer.SendOrWaitAsync(l).GetAwaiter().GetResult();

        }
    }
}