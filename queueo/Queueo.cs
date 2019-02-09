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
        public const int MaxConcurrentTasks = 500;
        public const int MaxBufferSize = 200000;

        static readonly ILog log = LogManager.GetLogger(typeof(NaiveQueueOrderingModel));

        /// <summary>
        /// To avoid OOM exceptions this limits the max tasks running at any one time
        /// </summary>
        private SemaphoreSlim buildQueueTasks = new SemaphoreSlim(MaxConcurrentTasks);

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
            buffer.ActionWhenQueueIsFlushed = (key, items) =>
            {
                var t = OutputQueueItems(items);
            };

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
                    await buildQueueTasks.WaitAsync();

                    var t = AssessQueueItemForBuffer(item);
                }

                item = NextQueueItem();
            }
            while (item != null);

            // dump out remainder queue items
            EmptyBuffer();

            while (_active > 0)
            {
                await Task.Delay(1000);
                log.InfoFormat("Waiting for output buffers to end, running {0}", _active);
            }

        }

        void CacheUri(QueueItem item) => lookupCache.Enqueue(item.TargetUri);

        bool UriIsNotADuplicate(QueueItem item) => item != null && !lookupCache.Contains(item.TargetUri);

        async Task AssessQueueItemForBuffer(QueueItem item)
        {
            try
            {
                IPAddress addr = await Utility.GetHostIPAddress(item.TargetUri);

                await CalculateQueueItemProperties(item);
                AddToBuffer(addr, item);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                buildQueueTasks.Release();
            }
        }

        private void AddToBuffer(IPAddress addr, QueueItem item)
        {
            lock (outputBufferLock) buffer.Add(addr, item);
        }

        private void EmptyBuffer()
        {
            lock (outputBufferLock) buffer.FlushAllQueues();
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
            try
            {
                Interlocked.Increment(ref _active);

                await Configuration.QueueOrderingModel.CalculatePriority(items);

                lock (outputBufferLock)
                {
                    foreach (var item in items.OrderBy(x => x.Priority))
                        Console.WriteLine(item);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        /// <summary>
        /// Fills in the properties of a queue item based on what we know
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        async Task CalculateQueueItemProperties(QueueItem item)
        {
            var needsVisitingTask = NeedsVisiting(item);
            item.UnsupportedUri = CantDownloadItYet(item);
            item.IsBlockedByDomain = IsDomainInALanguageICantRead(item);
            item.IsProbablyBlocked = IsUriProbablyBlocked(item);
            item.VisitedRecently = !await needsVisitingTask;
        }

        /// <summary>
        /// Returns true if theres no resource fetcher for the type of URL
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool CantDownloadItYet(QueueItem item) => !ResourceFetcher.HasHandler(item.TargetUri);

        /// <summary>
        /// Detects URIs that are probably blocked before we attempt to download them
        /// </summary>
        /// <param name="item"></param>
        /// <returns>True if the item is likely to be for an item blocked by the default block provider</returns>
        bool IsUriProbablyBlocked(QueueItem item) =>
            item == null ||
            item.TargetUri == null ||
            DefaultBlockProvider.IsProbablyBlocked(item.TargetUri);

        /// <summary>
        /// Block things I can't read
        /// </summary>
        /// <param name="item"></param>
        /// <returns>True if the queue item is probably for a site I can't read the language of</returns>
        bool IsDomainInALanguageICantRead(QueueItem item) =>
            item == null ||
            item.TargetUri.Host.EndsWith(".cn") ||
            item.TargetUri.Host.EndsWith(".jp") ||
            item.TargetUri.Host.EndsWith(".de") ||
            item.TargetUri.Host.EndsWith(".ru") ||
            item.TargetUri.Host.EndsWith(".it") ||
            item.TargetUri.Host.EndsWith(".eg") ||
            item.TargetUri.Host.EndsWith(".ez") ||
            item.TargetUri.Host.EndsWith(".iq") ||
            item.TargetUri.Host.EndsWith(".sa") ||
            item.TargetUri.Host.EndsWith(".hk") ||
            item.TargetUri.Host.EndsWith(".dz") ||
            item.TargetUri.Host.EndsWith(".vi") ||
            item.TargetUri.Host.EndsWith(".dz") ||
            item.TargetUri.Host.EndsWith(".id") ||
            item.TargetUri.Host.EndsWith(".fr") ||
            item.TargetUri.Host.EndsWith(".pl") ||
            item.TargetUri.Host.EndsWith(".es") ||
            item.TargetUri.Host.EndsWith(".mx") ||
            item.TargetUri.Host.EndsWith(".my") ||
            item.TargetUri.Host.EndsWith(".kr") ||
            item.TargetUri.Host.EndsWith(".ch") ||
            item.TargetUri.Host.EndsWith(".ro") ||
            item.TargetUri.Host.EndsWith(".be") ||
            item.TargetUri.Host.EndsWith(".se") ||
            item.TargetUri.Host.EndsWith(".br");

        /// <summary>
        /// Returns true if the item needs visiting according to our recency index
        /// </summary>
        /// <param name="item"></param>
        /// <returns>True if the queue item needs visiting</returns>
        async Task<bool> NeedsVisiting(QueueItem item)
        {
            try
            {
                bool rtn = false;

                using (var db = new Database())
                {
                    rtn = await db.NeedsVisiting(item.TargetUri);
                }

                return rtn;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return true;
            }
        }


    }
}

