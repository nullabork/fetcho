
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Fetcho.Common;
using log4net;

namespace Fetcho.NextLinks
{
    class NextLinks
    {
        /// <summary>
        /// Init all lists to this
        /// </summary>
        public const int MaxConcurrentTasks = 10000;

        /// <summary>
        /// Queue items with a number higher than this will be rejected 
        /// </summary>
        public const uint MaximumPriorityValueForLinks = 740 * 1000 * 1000;

        /// <summary>
        /// 
        /// </summary>
        public const int HowOftenToReportStatusInMilliseconds = 30000;

        /// <summary>
        /// Maximum links that can be output
        /// </summary>
        public const int MaximumLinkQuota = 400000;

        /// <summary>
        /// Number of items that can be in a chunk
        /// </summary>
        public const int MaximumChunkSize = 50;

        public const int MaxConcurrentFetches = 2000;

        /// <summary>
        /// Enable the quota
        /// </summary>
        public const bool QuotaEnabled = true;

        /// <summary>
        /// Log4Net logger
        /// </summary>
        static readonly ILog log = LogManager.GetLogger(typeof(NextLinks));

        /// <summary>
        /// I'm the real SemaphoreSlim ... shady
        /// </summary>
        /// <remarks>Limits the number of queue validations occurring at once to the initial buffer size to avoid downloading more robots than we need to</remarks>
        static readonly SemaphoreSlim taskPool = new SemaphoreSlim(MaxConcurrentTasks);

        private int _activeTasks = 0;

        public int LinksAccepted = 0;
        public int LinksRejected = 0;

        private readonly object acceptStreamLock = new object();
        private TextWriter acceptStream;
        private readonly object rejectStreamLock = new object();
        private TextWriter rejectStream;

        /// <summary>
        /// Configuration of this passed in when calling it
        /// </summary>
        public NextLinksConfiguration Configuration
        {
            get;
            protected set;
        }

        /// <summary>
        /// Create an object with the associated configuration
        /// </summary>
        /// <param name="config"></param>
        public NextLinks(NextLinksConfiguration config) => Configuration = config;

        public async Task Process()
        {

            try
            {
                acceptStream = GetAcceptStream();
                rejectStream = GetRejectStream();

                using (var reader = GetInputStream())
                {

                    var l = new List<QueueItem>();

                    QueueItem lastItem = null;
                    while (reader.Peek() >= 0)
                    {
                        // run through the queue items and group them into chunks by IP
                        // process those chunks collectively and throw out the entire 
                        // chunk if the IP has been seen recently
                        string line = reader.ReadLine();
                        var item = QueueItem.Parse(line);

                        if ( lastItem == null || !lastItem.TargetIP.Equals(item.TargetIP))
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

                while (true)
                {
                    await Task.Delay(HowOftenToReportStatusInMilliseconds).ConfigureAwait(false);
                    ReportStatus();
                    if (_activeTasks == 0)
                        return;
                }
            }
            catch (Exception ex)
            {
                log.Error("NextLinks", ex);
            }
        }

        void ReportStatus() => log.InfoFormat(
            "NEXTLINKS: Active Fetches {0}, Waiting to write {1}, Active Tasks {2}", 
            _activeTasks,
            ResourceFetcher.WaitingToWrite, 
            MaxConcurrentTasks - taskPool.CurrentCount
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
                AcceptLinks(accepted);
                RejectLinks(rejected);

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

        void AcceptLinks(IEnumerable<QueueItem> items) => OutputAcceptedLinks(items);

        void RejectLinks(IEnumerable<QueueItem> items) => OutputRejectedLinks(items);

        void OutputAcceptedLinks(IEnumerable<QueueItem> items)
        {
            lock (acceptStreamLock)
            {
                foreach( var item in items )
                {
                    LinksAccepted++;
                    acceptStream?.WriteLine(item.ToString());
                }

            }
        }

        void OutputRejectedLinks(IEnumerable<QueueItem> items)
        {
            lock (rejectStreamLock)
            {
                foreach( var item in items )
                {
                    LinksRejected++;
                    rejectStream?.WriteLine(item.ToString());
                }
            }
        }

        TextWriter GetRejectStream()
        {
            if (String.IsNullOrWhiteSpace(Configuration.RejectedLinkFilePath))
                return null;

            return new StreamWriter(Configuration.RejectedLinkFilePath, false);
        }

        /// <summary>
        /// The stream that accepted links get written out to
        /// </summary>
        /// <returns></returns>
        TextWriter GetAcceptStream() => Console.Out;

        /// <summary>
        /// Returns true if we've reached the maximum accepted links
        /// </summary>
        /// <returns></returns>
        bool QuotaReached() => QuotaEnabled && LinksAccepted >= MaximumLinkQuota;

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
        bool IsPriorityTooLow(QueueItem item) => item.Priority > MaximumPriorityValueForLinks;

        /// <summary>
        /// Throw out items when the queue gets too large
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool IsChunkSequenceTooHigh(QueueItem item) => item.ChunkSequence > MaximumChunkSize;

        // the function for the size of this window needs to be determined. *4 times the number of concurrent fetches is not correct
        // its probably relative to the number of unique IPs in a fetch window times the max-min length of their queues and a bunch 
        // of other factors
        FastLookupCache<string> recentips = new FastLookupCache<string>(MaxConcurrentFetches*4); 

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
                var r = await HostCacheManager.GetRobotsFile(item.TargetUri.Host);
                if (r == null) rtn = false;
                else if (r.IsDisallowed(item.TargetUri)) rtn = true;
            }
            catch (Exception ex)
            {
                log.Error("IsBlockedByRobots(): ", ex);
            }

            return rtn;
        }

        /// <summary>
        /// Get the appropriate input stream from the STDIN or command line argument
        /// </summary>
        /// <returns>An open TextReader object</returns>
        /// <remarks>The caller will have to dispose of the stream</remarks>
        TextReader GetInputStream()
        {
            if (String.IsNullOrWhiteSpace(Configuration.SourceLinkFilePath))
                return Console.In;

            var sr = new StreamReader(new FileStream(Configuration.SourceLinkFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));
            return sr;
        }
    }
}

