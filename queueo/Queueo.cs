using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fetcho.Common;
using log4net;

namespace Fetcho.queueo
{
    /// <summary>
    /// Calculation where in the queue the link should sit
    /// </summary>
    class Queueo
    {
        public const int InitialBufferSize = 10000;
        public const int FastCacheSize = 10000;
        public const int MaxConcurrentTasks = 100;

        static readonly ILog log = LogManager.GetLogger(typeof(NaiveQueueOrderingModel));

        private SemaphoreSlim taskPool = new SemaphoreSlim(MaxConcurrentTasks);
        private List<QueueItem> outputBuffer = new List<QueueItem>(InitialBufferSize);
        private readonly object outputBufferLock = new object();

        public QueueoConfiguration Configuration { get; set; }

        public Queueo(QueueoConfiguration config)
        {
            Configuration = config;
        }

        public async Task Process()
        {
            QueueItem item = null;
            Uri source = null;
            Uri target = null;
            Task t = null;

            var lookupCache = new FastLookupCache<Uri>(FastCacheSize); // put all the URLs into here will remove duplicates roughly

            var reader = Configuration.InStream;

            string line = reader.ReadLine();

            while (!String.IsNullOrWhiteSpace(line))
            {
                string[] tokens = line.Split('\t');

                if (tokens.Length >= 2 && Uri.IsWellFormedUriString(tokens[0], UriKind.Absolute) && Uri.IsWellFormedUriString(tokens[1], UriKind.Absolute))
                {
                    source = new Uri(tokens[0]);
                    target = new Uri(tokens[1]);

                    if (!lookupCache.Contains(target))
                    {
                        lookupCache.Enqueue(target);

                        item = new QueueItem()
                        {
                            SourceUri = source,
                            TargetUri = target
                        };

                        await taskPool.WaitAsync();
                        t = CalculateQueueSequenceNumber(item);
                    }
                }

                if (outputBuffer.Count >= InitialBufferSize)
                    OutputQueueItems();

                line = reader.ReadLine();
            }

            while (true)
            {
                await Task.Delay(10000);

                if (taskPool.CurrentCount == MaxConcurrentTasks)
                    break;

                if (outputBuffer.Count >= InitialBufferSize)
                    OutputQueueItems();
            }

            OutputQueueItems();
        }


        async Task CalculateQueueSequenceNumber(QueueItem item)
        {
            try
            {
                await Configuration.QueueOrderingModel.CalculateQueueSequenceNumber(item);
                lock (outputBufferLock) outputBuffer.Add(item);
            }
            catch( Exception ex )
            {
                log.Error(ex);
            }
            finally
            {
                taskPool.Release();
            }
        }

        void OutputQueueItems()
        {
            lock (outputBufferLock)
            {
                foreach (var item in outputBuffer.OrderBy(x => x.Sequence)) Console.WriteLine(item);
                outputBuffer.Clear();
            }
        }

    }
}

