using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Fetcho.Common;

namespace Fetcho.queueo
{
    /// <summary>
    /// Calculation where in the queue the link should sit
    /// </summary>
    class Queueo
    {
        public const int InitialBufferSize = 100000;
        public const int FastCacheSize = 10000;

        public QueueoConfiguration Configuration
        {
            get;
            set;
        }

        public Queueo(QueueoConfiguration config)
        {
            Configuration = config;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task Process()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var watch = new Stopwatch();
            var lookupCache = new FastLookupCache<Uri>(FastCacheSize); // put all the URLs into here will remove duplicates roughly
            var list = new List<QueueItem>(InitialBufferSize);
            var tasks = new List<Task>(InitialBufferSize);

            var reader = Configuration.InStream;

            string line = reader.ReadLine();

            while (!String.IsNullOrWhiteSpace(line) && list.Count < InitialBufferSize)
            {
                string[] tokens = line.Split('\t');

                if (tokens.Length >= 2 && Uri.IsWellFormedUriString(tokens[0], UriKind.Absolute) && Uri.IsWellFormedUriString(tokens[1], UriKind.Absolute))
                {
                    var source = new Uri(tokens[0]);
                    var target = new Uri(tokens[1]);

                    if (!lookupCache.Contains(target))
                    {
                        lookupCache.Enqueue(target);

                        var queueItem = new QueueItem()
                        {
                            SourceUri = source,
                            TargetUri = target
                        };

                        var calc = Configuration.QueueOrderingModel.CalculateQueueSequenceNumber(queueItem);

                        tasks.Add(calc);

                        list.Add(queueItem);
                    }
                }
                line = reader.ReadLine();
            }

            Task.WaitAll(tasks.ToArray());

            outputQueueItems(list.OrderBy(x => x.Sequence));
        }

        void outputQueueItems(IEnumerable<QueueItem> items)
        {
            foreach (var item in items) Console.WriteLine(item);
        }

    }
}

