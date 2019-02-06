using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fetcho.Common;
using log4net;

namespace Fetcho.queueo
{
    /// <summary>
    /// Calculates the crawl priority for links and eliminates duplicate links
    /// </summary>
    class Queueo
    {
        public const int FastCacheSize = 10000;
        public const int MaxConcurrentTasks = 100;
        public const int MaxBufferSize = 100000;

        static readonly ILog log = LogManager.GetLogger(typeof(NaiveQueueOrderingModel));

        /// <summary>
        /// To avoid OOM exceptions this limits the max tasks running at any one time
        /// </summary>
        private SemaphoreSlim taskPool = new SemaphoreSlim(MaxConcurrentTasks);

        /// <summary>
        /// Used to buffer queue items for output
        /// </summary>
        private List<QueueItem> outputBuffer = new List<QueueItem>(MaxBufferSize);

        /// <summary>
        /// Syncronise outputBuffer
        /// </summary>
        private readonly object outputBufferLock = new object();

        /// <summary>
        /// Configuration set for this class
        /// </summary>
        public QueueoConfiguration Configuration { get; set; }

        /// <summary>
        /// Create a Queueo with a configuration
        /// </summary>
        /// <param name="config"></param>
        public Queueo(QueueoConfiguration config) => Configuration = config;

        /// <summary>
        /// Start doing the work based on the configuration provided
        /// </summary>
        /// <returns></returns>
        public async Task Process()
        {
            // a limited cache that stores recent URLs to compare against for duplicates
            // prevents running out of memory whilst roughly eliminating duplicates cheaply
            // later stages of the process will properly limit dupes, but are more expensive
            // ie. have we recently visited the link?
            var lookupCache = new FastLookupCache<Uri>(FastCacheSize);

            var reader = Configuration.InStream;

            string line = reader.ReadLine();

            while (!String.IsNullOrWhiteSpace(line)) // empty line, end of stream
            {
                // get a queue item 
                QueueItem item = ExtractQueueItem(line);

                // if the item is OK and its not already cached
                if (item != null && !lookupCache.Contains(item.TargetUri))
                {
                    // cache it 
                    lookupCache.Enqueue(item.TargetUri);

                    // wait for access to the pool
                    await taskPool.WaitAsync();

                    // create a task to calculate its sequence number (expensive)
                    var t = CalculateQueueSequenceNumber(item);
                }

                // outputBuffer shouldn't exceed MaxBufferSize - if it does dump it out and clear it
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

            // dump out remainder queue items
            OutputQueueItems();
        }

        /// <summary>
        /// Start a task to calculate the sequence number for the queue <paramref name="item"/>
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        async Task CalculateQueueSequenceNumber(QueueItem item)
        {
            try
            {
                await Configuration.QueueOrderingModel.CalculatePriority(item);
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

        /// <summary>
        /// Gets a queue item from a raw <paramref name="line"/> - can determine if its a proper <see cref="QueueItem"/> or just raw link
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private QueueItem ExtractQueueItem(string line)
        {
            // if theres no data return null
            if (String.IsNullOrWhiteSpace(line)) return null;

            // if its a proper queue item parse it
            var item = QueueItem.Parse(line);

            // else try a version where it's not proper
            if (item == null)
            {
                string[] tokens = line.Split('\t');

                // if the tokens are both URIs make a bogus queue item
                if (tokens.Length >= 2 && 
                    Uri.IsWellFormedUriString(tokens[0], UriKind.Absolute) && 
                    Uri.IsWellFormedUriString(tokens[1], UriKind.Absolute))
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

        /// <summary>
        /// Outputs the queue items and clears the <see cref="outputBuffer"/>
        /// </summary>
        void OutputQueueItems()
        {
            lock (outputBufferLock)
            {
                foreach (var item in outputBuffer.OrderBy(x => x.Priority)) Console.WriteLine(item);
                outputBuffer.Clear();
            }
        }

    }
}

