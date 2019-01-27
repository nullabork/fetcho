using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
        public const int MaxConcurrentTasks = 1000;
        SemaphoreSlim taskPool = new SemaphoreSlim(MaxConcurrentTasks);

        public QueueoConfiguration Configuration
        {
            get;
            set;
        }

        public Queueo(QueueoConfiguration config)
        {
            Configuration = config;
        }

        public async Task Process()
        {
            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var lookupCache = new FastLookupCache<Uri>(FastCacheSize); // put all the URLs into here will remove duplicates roughly
            var list = new List<QueueItem>(InitialBufferSize);

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

                        var t = CalculateQueueSequenceNumber(queueItem, cancellationToken);

                        list.Add(queueItem);
                    }
                }
                line = reader.ReadLine();
            }

            while ( true)
            {
                await Task.Delay(10000, cancellationToken);
                if (taskPool.CurrentCount == MaxConcurrentTasks)
                    break;
            }

            OutputQueueItems(list.OrderBy(x => x.Sequence));
        }

        async Task CalculateQueueSequenceNumber(QueueItem item, CancellationToken cancellationToken)
        {
            await taskPool.WaitAsync(cancellationToken);
            await Configuration.QueueOrderingModel.CalculateQueueSequenceNumber(item, cancellationToken);
            taskPool.Release();
        }

        void OutputQueueItems(IEnumerable<QueueItem> items)
        {
            foreach (var item in items) Console.WriteLine(item);
        }

    }
}

