using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Fetcho.Common;
using log4net;

namespace Fetcho
{
    /// <summary>
    /// Calculates the crawl priority for links, eliminates duplicate links and puts the links into appropriate queues
    /// </summary>
    public class Queueo
    {
        public const int FastCacheSize = 10000;
        public const int MaxConcurrentTasks = 200;
        public const int MaxBufferSize = 200000;

        static readonly ILog log = LogManager.GetLogger(typeof(Queueo));

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
        /// Configuration set for this class
        /// </summary>
        public QueueoConfiguration Configuration { get; set; }

        public int _active = 0;

        public bool Running { get; set; }

        private BufferBlock<QueueItem> PrioritisationBufferIn = null;
        private BufferBlock<QueueItem> NextlinksBufferOut = null;
        private readonly SemaphoreSlim outBufferLock = new SemaphoreSlim(1);


        /// <summary>
        /// Create a Queueo with a configuration
        /// </summary>
        /// <param name="config"></param>
        public Queueo(QueueoConfiguration config, BufferBlock<QueueItem> prioritisationBufferIn, BufferBlock<QueueItem> nextlinksBufferOut) : this()
        {
            Configuration = config;
            PrioritisationBufferIn = prioritisationBufferIn;
            NextlinksBufferOut = nextlinksBufferOut;
        }

        public Queueo() => Running = true;

        /// <summary>
        /// Start doing the work based on the configuration provided
        /// </summary>
        /// <returns></returns>
        public async Task Process()
        {
            var r = ReportStatus();
            buffer.ActionWhenQueueIsFlushed = (key, items) =>
            {
                var t = SendQueueItemsToNextLinks(items);
            };

            // get a queue item 
            QueueItem item = await NextQueueItem();

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

                item = await NextQueueItem();
            }
            while (Running);

            // dump out remainder queue items
            while (buffer.ItemCount > 0 || _active > 0)
            {
                await Task.Delay(10000);
                log.InfoFormat("Waiting for output buffers to end, buffercount {0}, running {1}", buffer.ItemCount, _active);
                if (buffer.ItemCount > 0)
                    await EmptyBuffer();
            }

        }

        void Shutdown() => Running = false;

        void CacheUri(QueueItem item) => lookupCache.Enqueue(item.TargetUri);

        bool UriIsNotADuplicate(QueueItem item) => item != null && !lookupCache.Contains(item.TargetUri);

        async Task AssessQueueItemForBuffer(QueueItem item)
        {
            try
            {
                await CalculateQueueItemProperties(item);
                await AddToBuffer(item);
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

        private async Task AddToBuffer(QueueItem item)
        {
            try
            {
                await outBufferLock.WaitAsync();
                buffer.Add(item.TargetIP, item);
            }
            catch(Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                outBufferLock.Release();
            }
        }

        private async Task EmptyBuffer()
        {
            try
            {
                await outBufferLock.WaitAsync();
                buffer.FlushAllQueues();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                outBufferLock.Release();
            }
        }

        private async Task<QueueItem> NextQueueItem() => await PrioritisationBufferIn.ReceiveAsync().ConfigureAwait(false);

        private async Task ReportStatus()
        {
            while (true)
            {
                await Task.Delay(Configuration.HowOftenToReportStatusInMilliseconds);
                LogStatus("STATUS UPDATE");
            }
        }

        private void LogStatus(string status)
            => log.InfoFormat("{0}: Active outputs {1}, inbox {2}, queuing {3}, outbox {4}",
                status,
                _active,
                PrioritisationBufferIn.Count,
                buffer.ItemCount,
                NextlinksBufferOut.Count
                );

        /// <summary>
        /// Outputs the queue items and clears the <see cref="outputBuffer"/>
        /// </summary>
        async Task SendQueueItemsToNextLinks(IEnumerable<QueueItem> items)
        {
            try
            {
                Interlocked.Increment(ref _active);

                await Configuration.QueueOrderingModel.CalculatePriority(items).ConfigureAwait(false);
                await outBufferLock.WaitAsync().ConfigureAwait(false);

                int i = -1;
                foreach (var item in items.OrderBy(x => x.Priority))
                {
                    if (i == -1) i = lastItem != null && lastItem.TargetIP.Equals(item.TargetIP) ? lastItem.ChunkSequence : 0;
                    item.ChunkSequence = i++;
                    await SendQueueItemToNextLinks(item).ConfigureAwait(false);
                    lastItem = item;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                outBufferLock.Release();
                Interlocked.Decrement(ref _active);
            }
        }
        private QueueItem lastItem = null;


        /// <summary>
        /// Fills in the properties of a queue item based on what we know
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        async Task CalculateQueueItemProperties(QueueItem item)
        {
            item.UnsupportedUri = CantDownloadItYet(item);
            item.IsBlockedByDomain = IsDomainInALanguageICantRead(item);
            item.IsProbablyBlocked = IsUriProbablyBlocked(item);

            // cut the cost of this by 99% basically if these things are true
            if ( !item.IsBlockedByDomain && !item.UnsupportedUri && !item.IsProbablyBlocked )
            {
                item.TargetIP = await Utility.GetHostIPAddress(item.TargetUri);
                item.VisitedRecently = !await NeedsVisiting(item);
            }
        }

        async Task SendQueueItemToNextLinks(QueueItem item) => await NextlinksBufferOut.SendAsync(item).ConfigureAwait(false);

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
                    rtn = await db.NeedsVisiting(item.TargetUri).ConfigureAwait(false);
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

