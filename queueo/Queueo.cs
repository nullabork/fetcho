using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        public const int MaxBufferSize = 200000;

        static readonly ILog log = LogManager.GetLogger(typeof(NaiveQueueOrderingModel));

        /// <summary>
        /// To avoid OOM exceptions this limits the max tasks running at any one time
        /// </summary>
        private SemaphoreSlim taskPool = new SemaphoreSlim(MaxConcurrentTasks);

        /// <summary>
        /// Used to buffer queue items for output
        /// </summary>
        private QueueBuffer<IPAddress, QueueItem> buffer = new QueueBuffer<IPAddress, QueueItem>(MaxBufferSize / 50, 50);

        // a limited cache that stores recent URLs to compare against for duplicates
        // prevents running out of memory whilst roughly eliminating duplicates cheaply
        // later stages of the process will properly limit dupes, but are more expensive
        // ie. have we recently visited the link?
        FastLookupCache<Uri> lookupCache = new FastLookupCache<Uri>(FastCacheSize);


        /// <summary>
        /// Syncronise outputBuffer
        /// </summary>
        private readonly object outputBufferLock = new object();

        /// <summary>
        /// Configuration set for this class
        /// </summary>
        public QueueoConfiguration Configuration { get; set; }

        public int _active = 0;

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
            buffer.ActionWhenQueueIsFlushed = (key, items) => OutputQueueItems(items);

            // get a queue item 
            QueueItem item = NextQueueItem();

            do  
            {
                // if the item is OK and its not already cached
                if (UriIsNotADuplicate(item))
                {
                    // cache it 
                    CacheUri(item);

                    // wait for access to the pool
                    await taskPool.WaitAsync();

                    var t = AssessQueueItemForBuffer(item);
                }

                item = NextQueueItem();
            }
            while (item != null);

            // dump out remainder queue items
            EmptyBuffer();

            while (_active > 0)
                await Task.Delay(10);

        }

        void CacheUri(QueueItem item) => lookupCache.Enqueue(item.TargetUri);

        bool UriIsNotADuplicate(QueueItem item) => item != null && !lookupCache.Contains(item.TargetUri);

        async Task AssessQueueItemForBuffer(QueueItem item)
        {
            try
            {
                IPAddress addr = await Utility.GetHostIPAddress(item.TargetUri);

                AddToBuffer(addr, item);
            }
            catch( Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                taskPool.Release();
            }
        }

        private void AddToBuffer(IPAddress addr, QueueItem item)
        {
            lock(outputBufferLock) buffer.Add(addr, item);
        }

        private void EmptyBuffer()
        {
            lock(outputBufferLock) buffer.FlushAllQueues();
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

        private QueueItem NextQueueItem()
        {
            string line = Configuration.InStream.ReadLine();

            if (String.IsNullOrWhiteSpace(line))
                return null;

            // get a queue item 
            QueueItem item = ExtractQueueItem(line);

            if (item != null)
                return item;
            else
                return NextQueueItem();
        }

            /// <summary>
            /// Outputs the queue items and clears the <see cref="outputBuffer"/>
            /// </summary>
            async Task OutputQueueItems(IEnumerable<QueueItem> items)
        {
            Interlocked.Increment(ref _active);

            await Configuration.QueueOrderingModel.CalculatePriority(items);

            lock (outputBufferLock)
            {
                foreach (var item in items.OrderBy(x => x.Priority))
                    Console.WriteLine(item);
            }

            Interlocked.Decrement(ref _active);
        }

    }
}

