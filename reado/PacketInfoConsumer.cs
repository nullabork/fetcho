using Fetcho.Common;
using Nager.PublicSuffix;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Fetcho
{
    internal class PacketInfoConsumer : IWebDataPacketConsumer
    {
        public Uri CurrentUri;
        public ContentType ContentType;

        public string Name { get => "PacketInfoConsumer"; }
        public bool ProcessesRequest { get => true; }
        public bool ProcessesResponse { get => true; }
        public bool ProcessesException { get => true; }

        public bool Summarise { get; set; }

        public int ExceptionCount = 0;
        public int ResourceCount = 0;

        public bool PacketIsMalformed = false;
        public Exception MalformedException = null;

        public ulong CountOfDataBytes = 0;
        public ulong CountOfHeaderBytes = 0;
        public ulong CountOfRequestBytes = 0;

        public Dictionary<string, int> ContentTypes = new Dictionary<string, int>();
        public Dictionary<string, int> TLDCounts = new Dictionary<string, int>();
        public Dictionary<string, int> HostCounts = new Dictionary<string, int>();
        public Dictionary<string, int> ExceptionCounts = new Dictionary<string, int>();

        private DomainParser domainParser = new DomainParser(new WebTldRuleProvider());

        public PacketInfoConsumer(params string[] args)
        {
            Summarise = true;
            if (args.Any(x => x == "--all"))
                Summarise = false;
        }

        public void ProcessException(string exception)
        {
            if (WebDataPacketReader.IsException(exception))
            {
                ExceptionCount++;

                var classification = ExceptionClassifier.Classify(exception);

                Increment(ExceptionCounts, classification.ToString());
            }
        }

        public void ProcessRequest(string request)
        {
            CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);

            var domain = domainParser.Get(CurrentUri.Host);

            Increment(TLDCounts, domain == null ? "(blank)" : domain.TLD);
            Increment(HostCounts, CurrentUri?.Host);

            ResourceCount++;
            CountOfRequestBytes += (ulong)request.Length;
        }

        public void ProcessResponseHeaders(string responseHeaders)
        {
            CountOfHeaderBytes += (ulong)responseHeaders.Length;
            ContentType = WebDataPacketReader.GetContentTypeFromResponseHeaders(responseHeaders);

        }

        public void ProcessResponseStream(Stream dataStream)
        {
            bool guess = false;
            if (dataStream != null && ContentType.IsUnknownOrNull(ContentType))
            {
                guess = true;
                ContentType = ContentType.Guess(dataStream);
            }

            string key = String.Format("{0}{1}", guess ? "(GUESS) " : "", (ContentType ?? ContentType.Unknown).ToString());

            Increment(ContentTypes, key);
        }

        public void NewResource()
        {
            CurrentUri = null;
        }

        public void PacketClosed()
        {
            OutputDetails();
        }

        public void PacketOpened()
        {

        }

        public void ReadingException(Exception ex)
        {
            PacketIsMalformed = true;
            MalformedException = ex;
        }

        private void Increment<T>(Dictionary<T, int> dict, T k, int value = 1)
        {
            if (!dict.ContainsKey(k))
                dict.Add(k, 0);
            dict[k] += value;
        }

        private void OutputDetails()
        {
            Console.WriteLine("General");
            Console.WriteLine("\tResources:  \t{0}", ResourceCount);
            Console.WriteLine("\tExceptions: \t{0}\t{1}% of total", ExceptionCount, ExceptionCount * 100 / ResourceCount);
            Console.WriteLine("\tTLDs:       \t{0}", TLDCounts.Count);
            Console.WriteLine("\tHosts:      \t{0}", HostCounts.Count);
            Console.WriteLine("\tContent Types:\t{0}", ContentTypes.Count);
            Console.WriteLine();

            Console.WriteLine("Sizes");
            Console.WriteLine("\tRequests: \t{0} bytes", CountOfRequestBytes);
            Console.WriteLine("\tHeaders:  \t{0} bytes", CountOfHeaderBytes);
            Console.WriteLine("\tData:     \t{0} bytes", CountOfDataBytes);
            Console.WriteLine();

            OutputDictionary(ExceptionCounts, "Exceptions", 0);
            OutputDictionary(TLDCounts, "TLDs", ResourceCount / 100);
            OutputDictionary(ContentTypes, "Content Types", ResourceCount / 100);
            OutputDictionary(HostCounts, "Hosts", ResourceCount / 100);

            if (PacketIsMalformed)
            {
                Console.WriteLine("***PACKET IS MALFORMED***");
                Console.WriteLine("\tDetails: {0}", MalformedException.Message);
            }

        }

        void OutputDictionary(Dictionary<string, int> dict, string header, int threshold)
        {
            Console.WriteLine(header);
            foreach (var kvp in dict.OrderByDescending(x => x.Value))
                if (kvp.Value > threshold || !Summarise)
                    Console.WriteLine(
                        "\t{0}\t{1}", 
                        kvp.Value, 
                        String.IsNullOrWhiteSpace(kvp.Key.ToString()) ? "(Blank)" : kvp.Key.ToString());
            if ( Summarise)
                Console.WriteLine("\t...");
            Console.WriteLine("\t{0}\tTotal", dict.Count);
            Console.WriteLine();
        }
    }

}
