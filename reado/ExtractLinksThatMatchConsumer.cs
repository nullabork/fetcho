using Fetcho.Common;
using System;
using System.IO;

namespace Fetcho
{
    internal class ExtractLinksThatMatchConsumer : IWebDataPacketConsumer
    {
        public Uri CurrentUri;

        public string Name { get => "Extract Links that match"; }
        public bool ProcessesRequest { get => true; }
        public bool ProcessesResponse { get => true; }
        public bool ProcessesException { get => false; }

        public void ProcessException(string exception)
        {
        }

        public void ProcessRequest(string request) => CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);

        public void ProcessResponseHeaders(string responseHeaders)
        {
        }

        public void ProcessResponseStream(Stream dataStream)
        {
            using (var reader = new StreamReader(dataStream))
            {
                string line = reader.ReadLine();

                while (!reader.EndOfStream)
                {
                    if (line.ToLower().Contains("tesla"))
                    {
                        Console.WriteLine("{0}", CurrentUri);
                        return;
                    }

                    line = reader.ReadLine();
                }
            }
        }

        public void NewResource()
        {
            CurrentUri = null;
        }

        public void PacketClosed()
        {

        }

        public void PacketOpened()
        {

        }
    }
}
