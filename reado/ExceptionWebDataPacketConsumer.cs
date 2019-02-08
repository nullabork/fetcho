using Fetcho.Common;
using System;
using System.IO;

namespace Fetcho
{

    internal class ExceptionWebDataPacketConsumer : IWebDataPacketConsumer
    {
        public Uri CurrentUri;

        public string Name { get => "Extract Exceptions"; }
        public bool ProcessesRequest { get => true; }
        public bool ProcessesResponse { get => false; }
        public bool ProcessesException { get => true; }

        public void ProcessException(string exception)
        {
            if (WebDataPacketReader.IsException(exception))
            {
                Console.WriteLine("{0}", CurrentUri);
                Console.WriteLine(exception);
                Console.WriteLine();
            }
        }

        public void ProcessRequest(string request) => CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);

        public void ProcessResponseHeaders(string responseHeaders)
        {
        }

        public void ProcessResponseStream(Stream dataStream)
        {
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
