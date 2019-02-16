using Fetcho.Common;
using System;
using System.IO;

namespace Fetcho
{
    /// <summary>
    /// Used to find links that match specific queries
    /// </summary>
    internal class ExtractLinksThatMatchConsumer : IWebDataPacketConsumer
    {
        public FilterCollection IncludeFilters { get; }

        public Uri CurrentUri;

        public string Name { get => "Extract Links that match"; }
        public bool ProcessesRequest { get => true; }
        public bool ProcessesResponse { get => true; }
        public bool ProcessesException { get => false; }

        public ExtractLinksThatMatchConsumer(params string [] args)
        {
            IncludeFilters = new FilterCollection();
            foreach( string arg in args )
                IncludeFilters.Add(TextMatchFilter.Parse(arg));
        }

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
                    if (IncludeFilters.AllMatch(line))
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
        public void ReadingException(Exception ex) { }
    }


}
