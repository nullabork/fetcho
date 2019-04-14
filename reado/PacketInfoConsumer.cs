using Fetcho.Common;
using Nager.PublicSuffix;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Fetcho
{
    internal class PacketInfoConsumer : WebDataPacketConsumer
    {
        public Uri CurrentUri;
        public ContentType ContentType;

        public override string Name { get => "PacketInfoConsumer"; }
        public override bool ProcessesRequest { get => true; }
        public override bool ProcessesResponse { get => true; }
        public override bool ProcessesException { get => true; }

        public bool Summarise { get; set; }

        public int ExceptionCount = 0;
        public int ResourceCount = 0;

        public bool PacketIsMalformed = false;
        public Exception MalformedException = null;

        public ulong CountOfDataBytes = 0;
        public ulong CountOfHeaderBytes = 0;
        public ulong CountOfRequestBytes = 0;

        public double ResponseTimeMilliseconds = 0.0;

        public Dictionary<string, int> TLDCounts = new Dictionary<string, int>();
        public Dictionary<string, int> HostCounts = new Dictionary<string, int>();
        public Dictionary<string, int> ExceptionCounts = new Dictionary<string, int>();
        public Dictionary<string, int> ContentTypes = new Dictionary<string, int>();
        public Dictionary<string, int> ContentEncoding = new Dictionary<string, int>();
        public Dictionary<string, int> ContentLanguage = new Dictionary<string, int>();

        private DomainParser domainParser = new DomainParser(new WebTldRuleProvider());

        public PacketInfoConsumer(params string[] args)
        {
            Summarise = true;
            if (args.Any(x => x == "--all"))
                Summarise = false;
        }

        public override async Task ProcessException(string exception)
        {
            if (WebDataPacketReader.IsException(exception))
            {
                ExceptionCount++;

                var classification = ExceptionClassifier.Classify(exception);

                Increment(ExceptionCounts, classification.ToString());
            }
        }

        public override async Task ProcessRequest(string request)
        {
            CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);

            var domain = domainParser.Get(CurrentUri.Host);

            Increment(TLDCounts, domain == null ? "(blank)" : domain.TLD);
            Increment(HostCounts, CurrentUri?.Host);

            ResourceCount++;
            CountOfRequestBytes += (ulong)request.Length;

            var headers = WebDataPacketReader.GetHeaders(request);
            if (headers.ContainsKey("responsetime"))
                ResponseTimeMilliseconds += TimeSpan.Parse(headers["responsetime"]).TotalMilliseconds;
        }

        public override async Task ProcessResponseHeaders(string responseHeaders)
        {
            CountOfHeaderBytes += (ulong)responseHeaders.Length;
            ContentType = WebDataPacketReader.GetContentTypeFromResponseHeaders(responseHeaders);

            var headers = WebDataPacketReader.GetHeaders(responseHeaders);
            if ( headers.ContainsKey("content-encoding"))
                Increment(ContentEncoding, headers["content-encoding"].ToLower());
            else
                Increment(ContentEncoding, "(not specified)");

            if (headers.ContainsKey("content-language"))
                Increment(ContentLanguage, headers["content-language"].ToLower());
            else
                Increment(ContentLanguage, "(not specified)");


        }

        public override async Task ProcessResponseStream(Stream dataStream)
        {
            using (var ms = new MemoryStream())
            {
                dataStream?.CopyTo(ms);

                ms.Seek(0, SeekOrigin.Begin);

                CountOfDataBytes += (ulong)ms.Length;

                bool guess = false;
                if (ContentType.IsUnknownOrNull(ContentType))
                {
                    guess = true;
                    ContentType = ContentType.Guess(ms);
                }

                var ct = (ContentType ?? ContentType.Unknown);
                string key = String.Format("{0}{1}/{2}", guess ? "(GUESS) " : "", ct.MediaType, ct.SubType);

                Increment(ContentTypes, key);
            }
        }

        public override void NewResource()
        {
            CurrentUri = null;
        }

        public override void PacketClosed()
        {
            OutputDetails();
        }

        public override void ReadingException(Exception ex)
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
            Console.WriteLine("\tEncodings:\t{0}", ContentEncoding.Count);
            Console.WriteLine("\tLanguages:\t{0}", ContentLanguage.Count);
            Console.WriteLine("\tAvg Response Time:\t{0}ms", ResourceCount == 0 ? 0 : ResponseTimeMilliseconds / ResourceCount);
            Console.WriteLine();

            Console.WriteLine("Sizes");
            Console.WriteLine("\tRequests: \t{0}kb", CountOfRequestBytes / 1024);
            Console.WriteLine("\tHeaders:  \t{0}kb", CountOfHeaderBytes / 1024);
            Console.WriteLine("\tData:     \t{0}kb\t{1} avg kb/resource",
                                CountOfDataBytes / 1024,
                                ResourceCount == 0 ? 0 : CountOfDataBytes / 1024 / (ulong)(ResourceCount - ExceptionCount));
            Console.WriteLine();

            OutputDictionary(ExceptionCounts, "Exceptions", 0); // don't summarise any of the data for exceptions
            OutputDictionary(TLDCounts, "TLDs", ResourceCount / 100);
            OutputDictionary(HostCounts, "Hosts", ResourceCount / 100);
            OutputDictionary(ContentTypes, "Content Types", ResourceCount / 100);
            OutputDictionary(ContentEncoding, "Content Encoding", 0);
            OutputDictionary(ContentLanguage, "Content Language", ResourceCount / 100);

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
                        "\t{0}\t{1}%\t{2}",
                        kvp.Value,
                        ResourceCount == 0 ? 0 : kvp.Value / ResourceCount,
                        String.IsNullOrWhiteSpace(kvp.Key.ToString()) ? "(Blank)" : kvp.Key.ToString());
            if (Summarise)
                Console.WriteLine("\t...");
            Console.WriteLine("\t{0}\tTotal", dict.Count);
            Console.WriteLine();
        }
    }

}
