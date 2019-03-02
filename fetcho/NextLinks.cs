
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Fetcho.Common;
using log4net;

namespace Fetcho
{
    /// <summary>
    /// Goes through the queue items and decides which ones to pass through for fetching next
    /// </summary>
    public class NextLinks
    {

        /// <summary>
        /// Log4Net logger
        /// </summary>
        static readonly ILog log = LogManager.GetLogger(typeof(NextLinks));

        /// <summary>
        /// I'm the real SemaphoreSlim ... shady
        /// </summary>
        /// <remarks>Limits the number of queue validations occurring at once to the initial buffer size to avoid downloading more robots than we need to</remarks>
        private readonly SemaphoreSlim taskPool;

        private int _activeTasks = 0;

        public int LinksAccepted = 0;
        public int LinksRejected = 0;

        private BufferBlock<QueueItem> NextlinksBufferIn;
        private readonly SemaphoreSlim FetchQueueBufferOutLock = new SemaphoreSlim(1);
        private BufferBlock<QueueItem> FetchQueueBufferOut;
        private readonly SemaphoreSlim RejectsBufferOutLock = new SemaphoreSlim(1);
        private BufferBlock<QueueItem> RejectsBufferOut;

        /// <summary>
        /// Configuration of this passed in when calling it
        /// </summary>
        public NextLinksConfiguration Configuration { get; protected set; }

        public bool Running { get; set; }

        /// <summary>
        /// Create an object with the associated configuration
        /// </summary>
        /// <param name="config"></param>
        public NextLinks(
            NextLinksConfiguration config,
            BufferBlock<QueueItem> nextlinksBufferIn,
            BufferBlock<QueueItem> fetchQueueBufferOut,
            BufferBlock<QueueItem> rejectsBufferOut)
        {
            Configuration = config;
            Running = true;
            NextlinksBufferIn = nextlinksBufferIn;
            FetchQueueBufferOut = fetchQueueBufferOut;
            RejectsBufferOut = rejectsBufferOut;

            taskPool = new SemaphoreSlim(Configuration.MaxConcurrentTasks);
            recentips = new FastLookupCache<string>(Configuration.WindowForIPsSeenRecently);
        }


        public async Task Process()
        {

            try
            {
                var r = ReportStatus();
                var l = new List<QueueItem>();

                QueueItem lastItem = null;
                while (Running)
                {
                    // run through the queue items and group them into chunks by IP
                    // process those chunks collectively and throw out the entire 
                    // chunk if the IP has been seen recently
                    var item = await NextQueueItem();

                    if (lastItem == null || !lastItem.TargetIP.Equals(item.TargetIP))
                    {
                        var t = ValidateQueueItems(l.ToArray()).ConfigureAwait(false);

                        while (!await taskPool.WaitAsync(30000).ConfigureAwait(false))
                        {
                            //log.Info("Waiting to ValidateQueueItem");
                            if (QuotaReached())
                                break;
                        }

                        l = new List<QueueItem>();
                    }

                    l.Add(item);

                    lastItem = item;
                }
            }
            catch (Exception ex)
            {
                log.Error("NextLinks", ex);
            }
        }

        async Task ReportStatus()
        {
            while (true)
            {
                await Task.Delay(Configuration.HowOftenToReportStatusInMilliseconds);
                LogStatus("STATUS UPDATE");
            }
        }

        void LogStatus(string status) =>
        log.InfoFormat(
            "{0}: Active Fetches {1}, inbox {2}, Waiting to write {3}, Active Tasks {4}, outbox - fetching {5}, outbox - rejects {6}, accepted {7}, rejected {8}",
            status,
            _activeTasks,
            NextlinksBufferIn.Count,
            ResourceFetcher.WaitingToWrite,
            Configuration.MaxConcurrentTasks - taskPool.CurrentCount,
            FetchQueueBufferOut.Count,
            RejectsBufferOut.Count,
            LinksAccepted,
            LinksRejected
            );

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
                Interlocked.Increment(ref _activeTasks);

                QueueItem firstItem = items[0];
                for (int i = 0; i < items.Length; i++)
                {
                    QueueItem item = items[i];

                    if (item == null)
                    {
                        // item has an issue

                    }

                    else if (QuotaReached())
                    {
                        rejected.Add(item);
                    }

                    else if (item.HasAnIssue)
                    {
                        rejected.Add(item);
                    }

                    else if (IsMalformedQueueItem(item))
                    {
                        item.MalformedUrl = true;
                        rejected.Add(item);
                    }

                    else if (IsPriorityTooLow(item))
                    {
                        item.PriorityTooLow = true;
                        rejected.Add(item);
                    }

                    // make it so one chunk isn't too big
                    else if (IsChunkSequenceTooHigh(item))
                    {
                        rejected.Add(item);
                    }

                    // if seen IP recently
                    else if (HasIPBeenSeenRecently(item))
                    {
                        rejected.Add(item);
                    }

                    else if (await IsBlockedByRobots(item)) // most expensive one last
                    {
                        item.BlockedByRobots = true;
                        rejected.Add(item);
                    }

                    else
                    {
                        accepted.Add(item);
                    }
                }

                CacheRecentIPAddress(firstItem);
                await AcceptLinks(accepted).ConfigureAwait(false);
                await RejectLinks(rejected).ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                taskPool.Release();
                Interlocked.Decrement(ref _activeTasks);
            }
        }

        async Task AcceptLinks(IEnumerable<QueueItem> items) => await OutputAcceptedLinks(items).ConfigureAwait(false);

        async Task RejectLinks(IEnumerable<QueueItem> items) => await OutputRejectedLinks(items).ConfigureAwait(false);

        async Task OutputAcceptedLinks(IEnumerable<QueueItem> items)
        {
            try
            {
                await FetchQueueBufferOutLock.WaitAsync();
                foreach (var item in items)
                {
                    LinksAccepted++;
                    await FetchQueueBufferOut.SendAsync(item).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                FetchQueueBufferOutLock.Release();

            }
        }

        async Task OutputRejectedLinks(IEnumerable<QueueItem> items)
        {
            try
            {
                await RejectsBufferOutLock.WaitAsync();
                foreach (var item in items)
                {
                    LinksRejected++;
                    await RejectsBufferOut.SendAsync(item);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                RejectsBufferOutLock.Release();

            }
        }

        public void Shutdown() => Running = false;

        async Task<QueueItem> NextQueueItem() => await NextlinksBufferIn.ReceiveAsync();

        /// <summary>
        /// Returns true if we've reached the maximum accepted links
        /// </summary>
        /// <returns></returns>
        bool QuotaReached() => Configuration.QuotaEnabled && LinksAccepted >= Configuration.MaximumLinkQuota;

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
        bool IsPriorityTooLow(QueueItem item) => item.Priority > Configuration.MaximumPriorityValueForLinks;

        /// <summary>
        /// Throw out items when the queue for an IP gets too large
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool IsChunkSequenceTooHigh(QueueItem item) => item.ChunkSequence > Configuration.MaximumChunkSize;

        // the function for the size of this window needs to be determined. *4 times the number of concurrent fetches is not correct
        // its probably relative to the number of unique IPs in a fetch window times the max-min length of their queues and a bunch 
        // of other factors
        FastLookupCache<string> recentips = null;

        /// <summary>
        /// Check if an IP has been seen recently
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool HasIPBeenSeenRecently(QueueItem item) => recentips.Contains(item.TargetIP.ToString());

        /// <summary>
        /// Add an IP address to the fast lookup cache to say its been seen recently
        /// </summary>
        /// <param name="item"></param>
        void CacheRecentIPAddress(QueueItem item)
        {
            if (!recentips.Contains(item.TargetIP.ToString()))
                recentips.Enqueue(item.TargetIP.ToString());
        }

        /// <summary>
        /// Returns true if the queue item is blocked by a rule in the associated robots file
        /// </summary>
        /// <param name="item"></param>
        /// <returns>bool</returns>
        async Task<bool> IsBlockedByRobots(QueueItem item)
        {
            bool rtn = false;

            try
            {
                var r = await HostCacheManager.GetRobotsFile(item.TargetUri.Host).ConfigureAwait(false);
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
    }
}

