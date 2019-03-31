using Fetcho.Common;
using System;

namespace Fetcho
{

    internal class ExceptionWebDataPacketConsumer : WebDataPacketConsumer
    {
        public Uri CurrentUri;

        public override string Name { get => "Extract Exceptions"; }
        public override bool ProcessesRequest { get => true; }
        public override bool ProcessesException { get => true; }

        public ExceptionClassification Filter { get; set; }

        public ExceptionWebDataPacketConsumer(params string[] args)
        {
            Filter = ExceptionClassification.NotClassified;
            if (args.Length > 0 && Enum.TryParse(args[0], out ExceptionClassification r))
                Filter = r;

        }

        public override void ProcessException(string exception)
        {
            if (!WebDataPacketReader.IsException(exception)) return;

            if (!IncludeThisOne(exception)) return;

            Console.WriteLine("{0}", CurrentUri);
            Console.WriteLine(exception);
            Console.WriteLine();
        }

        public override void ProcessRequest(string request) 
            => CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);

        public override void NewResource()
        {
            CurrentUri = null;
        }

        private bool IncludeThisOne(string exception)
        {
            if (Filter == ExceptionClassification.NotClassified) return true;

            var classification = ExceptionClassifier.Classify(exception);

            return classification == Filter;
        }
    }
}
