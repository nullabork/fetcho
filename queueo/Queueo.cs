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
        public const int FastCacheSize = 10000;
        public const int MaxConcurrentTasks = 100;
        public const int MaxBufferSize = 100000;

        static readonly ILog log = LogManager.GetLogger(typeof(NaiveQueueOrderingModel));

        private SemaphoreSlim taskPool = new SemaphoreSlim(MaxConcurrentTasks);
        private List<QueueItem> outputBuffer = new List<QueueItem>(MaxBufferSize);
        private readonly object outputBufferLock = new object();

        public QueueoConfiguration Configuration { get; set; }

        public Queueo(QueueoConfiguration config)
        {
            Configuration = config;
        }

        public async Task Process()
        {
            Task t = null;

            var lookupCache = new FastLookupCache<Uri>(FastCacheSize); // put all the URLs into here will remove duplicates roughly

            var reader = Configuration.InStream;

            string line = reader.ReadLine();

            while (!String.IsNullOrWhiteSpace(line))
            {
                QueueItem item = ExtractQueueItem(line);

                if (item != null && !lookupCache.Contains(item.TargetUri))
                {
                    lookupCache.Enqueue(item.TargetUri);
                    await taskPool.WaitAsync();
                    t = CalculateQueueSequenceNumber(item);
                }

                if (outputBuffer.Count >= MaxBufferSize)
                    OutputQueueItems();

                line = reader.ReadLine();
            }

            while (true)
            {
                await Task.Delay(10000);

                if (taskPool.CurrentCount == MaxConcurrentTasks)
                    break;

                if (outputBuffer.Count >= MaxBufferSize)
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
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                taskPool.Release();
            }
        }

        QueueItem ExtractQueueItem(string line)
        {
            // if theres no data return null
            if (String.IsNullOrWhiteSpace(line)) return null;

            // if its a proper queue item parse it
            var item = QueueItem.Parse(line);

            // else try a version where it's not proper
            if (item == null)
            {
                string[] tokens = line.Split('\t');

                if (tokens.Length >= 2 && Uri.IsWellFormedUriString(tokens[0], UriKind.Absolute) && Uri.IsWellFormedUriString(tokens[1], UriKind.Absolute))
                {
                    var source = new Uri(tokens[0]);
                    var target = new Uri(tokens[1]);

                    item = new QueueItem()
                    {
                        SourceUri = source,
                        TargetUri = target
                    };
                }
            }

            if (item?.TargetUri == null)
                return null;
            return item;
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

