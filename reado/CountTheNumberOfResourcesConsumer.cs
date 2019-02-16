using Fetcho.Common;
using System;
using System.IO;

namespace Fetcho
{
    internal class CountTheNumberOfResourcesConsumer : IWebDataPacketConsumer
    {
        public int ResourceCount { get; set; }
        public int ExceptionCount { get; set; }

        public string Name { get => "Number of resources"; }
        public bool ProcessesRequest { get => true; }
        public bool ProcessesResponse { get => false; }
        public bool ProcessesException { get => true; }

        public void ProcessException(string exception) { if (WebDataPacketReader.IsException(exception)) ExceptionCount++; }

        public void ProcessRequest(string request) => ResourceCount++;

        public void ProcessResponseHeaders(string responseHeaders)
        {
        }

        public void ProcessResponseStream(Stream dataStream)
        {
        }

        public void NewResource()
        {
        }

        public void PacketClosed()
        {
            Console.WriteLine("Number of resources {0}, {1} were exceptions", ResourceCount, ExceptionCount);
        }

        public void PacketOpened()
        {

        }

        public void ReadingException(Exception ex) { }
    }
}
