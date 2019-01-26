using System;
using System.IO;

namespace Fetcho.queueo
{
	class QueueoConfiguration
	{
		public string DataSourceFilePath {
			get;
			set;
		}

		public IQueueCalculationModel QueueOrderingModel {
			get;
			set;
		}

        public TextReader InStream { get; set; }

        public TextWriter OutStream { get; set; }

		public QueueoConfiguration(string[] args)
		{
			if (args.Length > 0)
				DataSourceFilePath = args[0];

            InStream = getInputStream();
            OutStream = Console.Out;
		}

        public QueueoConfiguration()
        {
        }

        TextReader getInputStream()
        {
            if (String.IsNullOrWhiteSpace(DataSourceFilePath))
                return Console.In;

            var fs = new FileStream(DataSourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var sr = new StreamReader(fs);
            return sr;
        }

    }
}

