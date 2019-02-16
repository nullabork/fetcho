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
            Console.WriteLine(CurrentUri);
            if ( ContentType.MediaType == "text")
            {
                using (var tokenizer = new WordTokenizer(new HtmlTextReader(dataStream), true))
                {
                    foreach (var word in tokenizer)
                        Console.Write(word + " ");
                }
            }

            Console.WriteLine();
        }

        public void NewResource()
        {
            CurrentUri = null;
            ContentType = null;
        }

        public void PacketClosed() { }

        public void PacketOpened() { }

        public void ReadingException(Exception ex) { }
    }

}
