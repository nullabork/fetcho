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
        public const int FastCacheSize = 1000000;

        static readonly ILog log = LogManager.GetLogger(typeof(Queueo));

        /// <summary>
        /// To avoid OOM exceptions this limits the max tasks running at any one time
        /// </summary>
        private SemaphoreSlim TaskPool = null;

        /// <summary>
        /// Used to buffer queue items for output
        /// </summary>
        private QueueBuffer<IPAddress, QueueItem> buffer = null;

        // a limited cache that stores recent URLs to compare against for duplicates
        // prevents running out of memory whilst roughly eliminating duplicates cheaply
        // later stages of the process will properly limit dupes, but are more expensive
        // ie. have we recently visited the link?
        private FastLookupCache<Uri> lookupCache;

        public bool Running { get; set; }

        private ISourceBlock<IEnumerable<QueueItem>> PrioritisationBufferIn = null;
        private readonly SemaphoreSlim RejectsBufferOutLock = new SemaphoreSlim(1);
        private ITargetBlock<IEnumerable<QueueItem>> RejectsBufferOut = null;
        private readonly SemaphoreSlim FetchQueueBufferOutLock = new SemaphoreSlim(1);
        private ITargetBlock<IEnumerable<QueueItem>> FetchQueueBufferOut;

        public int _duplicates = 0;
        public int ActiveValidationTasks = 0;
        public int LinksAccepted = 0;
        public int LinksRejected = 0;

        /// <summary>
        /// Create a Queueo with a configuration
        /// </summary>
        /// <param name="config"></param>
        public Queueo(
            ISourceBlock<IEnumerable<QueueItem>> prioritisationBufferIn,
            ITargetBlock<IEnumerable<QueueItem>> fetchQueueBufferOut,
            ITargetBlock<IEnumerable<QueueItem>> rejectsBufferOut) : this()
        {
            PrioritisationBufferIn = prioritisationBufferIn;
            RejectsBufferOut = rejectsBufferOut;
            FetchQueueBufferOut = fetchQueueBufferOut;

            buffer = BuildQueueBuffer();
            recentips = BuildRecentIPsCache();
            TaskPool = new SemaphoreSlim(FetchoConfiguration.Current.MaxConcurrentTasks);
            lookupCache = new FastLookupCache<Uri>(FetchoConfiguration.Current.DuplicateLinkCacheWindowSize);

            FetchoConfiguration.Current.ConfigurationChange += (sender, e) => UpdateConfigurationSettings(e);
        }

        public Queueo() => Running = true;

        /// <summary>
        /// Start doing the work based on the configuration provided
        /// </summary>
        /// <returns></returns>
        public async Task Process()
        {
            try
            {

                var r = ReportStatus();
                buffer.ActionWhenQueueIsFlushed = (key, items) =>
                {
                    var t = ValidateQueueItems(items.ToArray());
                };

                do
                {
                    // get a queue items 
                    var items = await PrioritisationBufferIn.ReceiveAsync().ConfigureAwait(false);

                    // wait for tasks to continue
                    await TaskPool.WaitAsync().ConfigureAwait(false);

                    var tasks = new List<Task>();

                    foreach (var item in items)
                    {
                        // if the item is OK and its not already cached
                        if (UriIsNotADuplicate(item))
                        {
                            // status
                            item.Status = QueueItemStatus.Queuing;

                            // cache it 
                            CacheUri(item);

                            // release() happens in this task
                            tasks.Add(AssessQueueItemForBuffer(item));
                        }
                        else
                        {
                            item.Status = QueueItemStatus.Duplicate;
                            _duplicates++;
                        }
                    }

                    var t = Task.WhenAll(tasks.ToArray()).ContinueWith((task) => TaskPool.Release());
                }
                while (Running);
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
                Environment.Exit(1);
            }
        }

        void Shutdown() => Running = false;

        void CacheUri(QueueItem item) => lookupCache.Enqueue(item.TargetUri);

        bool UriIsNotADuplicate(QueueItem item) => item != null && !lookupCache.Contains(item.TargetUri);

        private async Task AssessQueueItemForBuffer(QueueItem item)
        {
            try
            {
                item.UnsupportedUri = CantDownloadItYet(item);
                item.IsBlockedByDomain = IsDomainInALanguageICantRead(item);
                item.IsProbablyBlocked = IsUriProbablyBlocked(item);

                // cut the cost of this by 99% basically if these things are true
                if (!item.IsBlockedByDomain && !item.UnsupportedUri && !item.IsProbablyBlocked)
                {
                    item.TargetIP = await Utility.GetHostIPAddress(item.TargetUri);

                    if (!IPAddress.None.Equals(item.TargetIP))
                        item.VisitedRecently = !await NeedsVisiting(item);
                }

                if (RejectItemEarly(item))
                    SendItemToRejects(item);
                else
                    await AddToBuffer(item);

                if (outbuffer.Count >= 1000)
                    await SendBufferToRejects();

            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
        }

        private async Task AddToBuffer(QueueItem item)
        {
            try
            {
                await FetchQueueBufferOutLock.WaitAsync();
                buffer.Add(item.TargetIP, item);
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
            finally
            {
                FetchQueueBufferOutLock.Release();
            }
        }

        private async Task ReportStatus()
        {
            while (true)
            {
                await Task.Delay(FetchoConfiguration.Current.HowOftenToReportStatusInMilliseconds);
                LogStatus("STATUS UPDATE");
            }
        }

        private void LogStatus(string status)
            => log.InfoFormat("{0}: inbox {1}, duplicates {2}, inqueue {3}, validating {4}, to-rejects {5}, to-fetcho {6}, accepted {7}, rejected {8}",
                status,
                (PrioritisationBufferIn as BufferBlock<IEnumerable<QueueItem>>)?.Count,
                _duplicates,
                buffer.ItemCount,
                ActiveValidationTasks,
                (RejectsBufferOut as BufferBlock<IEnumerable<QueueItem>>)?.Count,
                (FetchQueueBufferOut as BufferBlock<IEnumerable<QueueItem>>)?.Count,
                LinksAccepted,
                LinksRejected
                );


        List<QueueItem> outbuffer = new List<QueueItem>();
        void SendItemToRejects(QueueItem item)
        {
            item.Status = QueueItemStatus.Discarded;
            outbuffer.Add(item);
        }

        async Task SendBufferToRejects()
        {
            await RejectLinks(outbuffer.ToArray());
            outbuffer.Clear();
        }

        bool RejectItemEarly(QueueItem item) =>
            item.IsBlockedByDomain ||
            item.IsProbablyBlocked ||
            item.UnsupportedUri ||
            item.TargetIP == null ||
            item.TargetIP.Equals(IPAddress.None);

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

                var db = await DatabasePool.GetDatabaseAsync();
                rtn = await db.NeedsVisiting(item.TargetUri).ConfigureAwait(false);
                await DatabasePool.GiveBackToPool(db);

                return rtn;
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
                return true;
            }
        }

        /// <summary>
        /// Asyncronously validates a QueueItem
        /// </summary>
        /// <param name="item">The queue item</param>
        /// <returns>Async task (no return value)</returns>
        async Task ValidateQueueItems(QueueItem[] items)
        {
            if (items == null || items.Length == 0) return;

            var accepted = new List<QueueItem>();
            var rejected = new List<QueueItem>();

            try
            {
                await TaskPool.WaitAsync().ConfigureAwait(false);
                Interlocked.Increment(ref ActiveValidationTasks);

                FetchoConfiguration.Current.QueueOrderingModel.CalculatePriority(items);

                int j = -1;
                foreach (var item in items.OrderBy(x => x.Priority))
                {
                    if (j == -1) j = lastItem != null && lastItem.TargetIP.Equals(item.TargetIP) ? lastItem.ChunkSequence : 0;
                    item.ChunkSequence = j++;
                    lastItem = item;
                }

                QueueItem firstItem = items[0];
                foreach (var item in items)
                {
                    if (QuotaReached())
                    {
                        item.Status = QueueItemStatus.Discarded;
                        rejected.Add(item);
                    }

                    else if (IsMalformedQueueItem(item))
                    {
                        item.MalformedUrl = true;
                        item.Status = QueueItemStatus.Discarded;
                        rejected.Add(item);
                    }

                    else if (IsPriorityTooLow(item))
                    {
                        item.PriorityTooLow = true;
                        item.Status = QueueItemStatus.Discarded;
                        rejected.Add(item);
                    }

                    // make it so one chunk isn't too big
                    else if (IsChunkSequenceTooHigh(item))
                    {
                        item.ChunkSequenceTooHigh = true;
                        item.Status = QueueItemStatus.Discarded;
                        rejected.Add(item);
                    }

                    // if seen IP recently
                    else if (HasIPBeenSeenRecently(item))
                    {
                        item.IPSeenRecently = true;
                        item.Status = QueueItemStatus.Discarded;
                        rejected.Add(item);
                    }

                    else if (await IsBlockedByRobots(item)) // most expensive one last
                    {
                        item.BlockedByRobots = true;
                        item.Status = QueueItemStatus.Discarded;
                        rejected.Add(item);
                    }

                    else
                    {
                        item.Status = QueueItemStatus.Fetching;
                        accepted.Add(item);
                    }
                }

                if (accepted.Count > 0)
                {
                    CacheRecentIPAddress(firstItem);
                    await AcceptLinks(accepted).ConfigureAwait(false);
                }
                await RejectLinks(rejected).ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
            finally
            {
                Interlocked.Decrement(ref ActiveValidationTasks);
                TaskPool.Release();
            }
        }
        private QueueItem lastItem = null;

        async Task AcceptLinks(IEnumerable<QueueItem> items)
        {
            Interlocked.Add(ref LinksAccepted, items.Count());
            await FetchQueueBufferOut.SendOrWaitAsync(items).ConfigureAwait(false);
        }

        async Task RejectLinks(IEnumerable<QueueItem> items)
        {
            Interlocked.Add(ref LinksRejected, items.Count());
            await RejectsBufferOut.SendOrWaitAsync(items).ConfigureAwait(false);
        }


        /// <summary>
        /// Returns true if we've reached the maximum accepted links
        /// </summary>
        /// <returns></returns>
        bool QuotaReached() => FetchoConfiguration.Current.QuotaEnabled && LinksAccepted >= FetchoConfiguration.Current.MaxLinkQuota;

        /// <summary>
        /// Returns true if the queue item is malformed and of no use to us
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool IsMalformedQueueItem(QueueItem item) => String.IsNullOrWhiteSpace(item.TargetUri.Host);

        /// <summary>
        /// Returns true if the queue item sequence is too high
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        /// <remarks>A high sequence number means the item has probably been visited recently or is not valid</remarks>
        bool IsPriorityTooLow(QueueItem item) => item.Priority > FetchoConfiguration.Current.MaxPriorityValueForLinks;

        /// <summary>
        /// Throw out items when the queue for an IP gets too large
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool IsChunkSequenceTooHigh(QueueItem item) => item.ChunkSequence > FetchoConfiguration.Current.MaxQueueBufferQueueLength;

        // the function for the size of this window needs to be determined. *4 times the number of concurrent fetches is not correct
        // its probably relative to the number of unique IPs in a fetch window times the max-min length of their queues and a bunch 
        // of other factors
        FastLookupCache<string> recentips = null;

        readonly object recentipsLock = new object();

        /// <summary>
        /// Check if an IP has been seen recently
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool HasIPBeenSeenRecently(QueueItem item)
        {
            lock (recentipsLock)
                return recentips.Contains(item.TargetIP.ToString());
        }

        /// <summary>
        /// Add an IP address to the fast lookup cache to say its been seen recently
        /// </summary>
        /// <param name="item"></param>
        void CacheRecentIPAddress(QueueItem item)
        {
            lock (recentipsLock)
                if (!recentips.Contains(item.TargetIP.ToString()))
                    recentips.Enqueue(item.TargetIP.ToString());
        }

        /// <summary>
        /// Returns true if the queue item is blocked by a rule in the associated robots file
        /// </summary>
        /// <param name="item"></param>
        /// <returns>bool</returns>
        async ValueTask<bool> IsBlockedByRobots(QueueItem item)
        {
            bool rtn = false;

            try
            {
                var r = await FetchoConfiguration.Current.HostCache.GetRobotsFile(item.TargetUri.Host).ConfigureAwait(false);
                if (r == null) rtn = false;
                else rtn = r.IsDisallowed(item.TargetUri);
            }
            catch (Exception ex)
            {
                log.Error("IsBlockedByRobots(): ", ex);
                rtn = false;
            }

            return rtn;
        }

        private FastLookupCache<string> BuildRecentIPsCache()
            => new FastLookupCache<string>(FetchoConfiguration.Current.WindowForIPsSeenRecently);

        private QueueBuffer<IPAddress, QueueItem> BuildQueueBuffer()
            => new QueueBuffer<IPAddress, QueueItem>(
                 FetchoConfiguration.Current.MaxQueueBufferQueues,
                 FetchoConfiguration.Current.MaxQueueBufferQueueLength
               );

        private void UpdateConfigurationSettings(ConfigurationChangeEventArgs e)
        {
            e.IfPropertyIs(
                 () => FetchoConfiguration.Current.MaxConcurrentTasks,
                 () => UpdateTaskPoolConfiguration(e)
            );

            e.IfPropertyIs(
                 () => FetchoConfiguration.Current.WindowForIPsSeenRecently,
                 () => recentips = BuildRecentIPsCache()
            );

            e.IfPropertyIs(
                 () => FetchoConfiguration.Current.MaxQueueBufferQueues,
                 () => buffer = BuildQueueBuffer()
            );

            e.IfPropertyIs(
                 () => FetchoConfiguration.Current.MaxQueueBufferQueueLength,
                 () => buffer = BuildQueueBuffer()
            );
        }

        private void UpdateTaskPoolConfiguration(ConfigurationChangeEventArgs e)
            => TaskPool.ReleaseOrReduce((int)e.OldValue, (int)e.NewValue).GetAwaiter().GetResult();

    }
}

