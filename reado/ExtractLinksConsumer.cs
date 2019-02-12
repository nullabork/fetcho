using Fetcho.Common;
using Fetcho.ContentReaders;
using System;
using System.IO;

namespace Fetcho
{
    internal class ExtractLinksConsumer : IWebDataPacketConsumer
    {
        public Uri CurrentUri;
        public string ContentType;

        public string Name { get => "Extract Links"; }
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
            var extractor = new TextFileLinkExtractor(CurrentUri, new StreamReader(dataStream));
            if (extractor != null)
                OutputUris(extractor);
        }

        public void NewResource()
        {
            CurrentUri = null;
            ContentType = String.Empty;
        }

        public void PacketClosed()
        {

        }

        public void PacketOpened()
        {

        }

        private ILinkExtractor GuessLinkExtractor(Stream dataStream)
        {
            if (ContentType.StartsWith("text/"))
                return new TextFileLinkExtractor(CurrentUri, new StreamReader(dataStream));
            else 
                return null;
        }

        private void OutputUris(ILinkExtractor reader)
        {
            if (reader == null) return;

            Uri uri = reader.NextUri();

            while (uri != null)
            {
                Console.WriteLine("{0}\t{1}", reader.CurrentSourceUri, uri);

                uri = reader.NextUri();
            }
        }
    }

}
