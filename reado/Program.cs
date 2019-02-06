using Fetcho.Common;
using System;
using System.IO;

namespace Fetcho
{
    class Program
    {
        static void Main(string[] args)
        {

            // args1 = Packet Procssor
            // args2 = Packet Path

            string packetProcessorString = args[0];
            string packetPath = args[1];

            var extractor = new ExceptionExtractor();
            extractor.Process(packetPath);

        }
    }

    public class ExceptionExtractor
    {
        private WebDataPacketProcessor Processor;

        public ExceptionExtractor()
        {
            Processor = new WebDataPacketProcessor();
        }

        public void Process(string filepath)
        {
            Processor.Consumer = new CountTheNumberOfResourcesConsumer();

            using (var fs = new FileStream(filepath, FileMode.Open))
            {
                var packet = new WebDataPacketReader(fs);
                Processor.Process(packet);
            }
        }

        private class ExceptionWebDataPacketConsumer : IWebDataPacketConsumer
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

        private class ExtractLinksThatMatchConsumer : IWebDataPacketConsumer
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

                    while ( !reader.EndOfStream )
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

        private class CountTheNumberOfResourcesConsumer : IWebDataPacketConsumer
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
        }
    }
}
