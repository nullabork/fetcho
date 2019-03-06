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

        public ExceptionClassification Filter { get; set; }

        public ExceptionWebDataPacketConsumer(params string[] args)
        {
            Filter = ExceptionClassification.NotClassified;
            if (args.Length > 0 && Enum.TryParse(args[0], out ExceptionClassification r))
                Filter = r;

        }

        public void ProcessException(string exception)
        {
            if (!WebDataPacketReader.IsException(exception)) return;

            if (!IncludeThisOne(exception)) return;

            Console.WriteLine("{0}", CurrentUri);
            Console.WriteLine(exception);
            Console.WriteLine();
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

        public void PacketClosed() { }

        public void PacketOpened() { }

        public void ReadingException(Exception ex) { }

        private bool IncludeThisOne(string exception)
        {
            if (Filter == ExceptionClassification.NotClassified) return true;

            var classification = ExceptionClassifier.Classify(exception);

            return classification == Filter;
        }
    }
}
