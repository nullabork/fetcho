using Fetcho.Common;
using Fetcho.ContentReaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Fetcho
{
    internal class ExtractLinksAndBufferConsumer : WebDataPacketConsumer
    {
        public Uri CurrentUri;
        public ContentType ContentType;
        public BufferBlock<IEnumerable<QueueItem>> PrioritisationBuffer;
        public long LinksExtracted;

        public ExtractLinksAndBufferConsumer(BufferBlock<IEnumerable<QueueItem>> prioritisationBuffer)
        {
            PrioritisationBuffer = prioritisationBuffer;
        }

        public override string Name { get => "Extract Links"; }
        public override bool ProcessesRequest { get => true; }
        public override bool ProcessesResponse { get => true; }

        public override async Task ProcessRequest(string request)
            => CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);

        public override async Task ProcessResponseHeaders(string responseHeaders)
            => ContentType = WebDataPacketReader.GetContentTypeFromResponseHeaders(responseHeaders);

        public override async Task ProcessResponseStream(Stream dataStream)
        {
            try
            {
                if (dataStream == null) return;
                var extractor = GuessLinkExtractor(dataStream);
                if (extractor != null)
                {
                    await SendUris(extractor);
                    extractor.Dispose();
                    extractor = null;
                }

            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
        }

        public override void NewResource()
        {
            CurrentUri = null;
            ContentType = null;
        }

        public override void ReadingException(Exception ex)
            => Utility.LogException(ex);

        private ILinkExtractor GuessLinkExtractor(Stream dataStream)
        {
            var ms = new MemoryStream();
            dataStream.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);

            if (ContentType.IsUnknownOrNull(ContentType))
                ContentType = ContentType.Guess(ms);

            ms.Seek(0, SeekOrigin.Begin);

            if (ContentType.IsHtmlContentType(ContentType))
                return new HtmlFileLinkExtractor(CurrentUri, ms);
            else if (ContentType.IsXmlContentType(ContentType))
                return new TextFileLinkExtractor(CurrentUri, new StreamReader(ms));
            else if (ContentType.IsTextContentType(ContentType))
                return new TextFileLinkExtractor(CurrentUri, new StreamReader(ms));
            else if (ContentType.IsUnknownOrNull(ContentType))
                return new TextFileLinkExtractor(CurrentUri, new StreamReader(ms));
            else
            {
                ms.Dispose();
                ms = null;
                //Utility.LogInfo("No link extractor for content type: {0}, from {1}", ContentType, CurrentUri);
                return null;
            }
        }

        private async Task SendUris(ILinkExtractor reader)
        {
            var l = new List<QueueItem>();
            if (reader == null) return;

            Uri uri = reader.NextUri();

            while (uri != null && l.Count < FetchoConfiguration.Current.MaxLinksToExtractFromOneResource*10)
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
            // effectively block until the URLs are accepted
            await PrioritisationBuffer.SendOrWaitAsync(l.Randomise().Take(FetchoConfiguration.Current.MaxLinksToExtractFromOneResource)).ConfigureAwait(false);

        }
    }
}