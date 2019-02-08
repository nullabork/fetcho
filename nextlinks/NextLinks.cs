
using System;
using System.Diagnostics;
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
        public const int MaxConcurrentTasks = 30000;

        /// <summary>
        /// Queue items with a number higher than this will be rejected 
        /// </summary>
        public const uint MaximumSequenceForLinks = 500*1000*1000;

        /// <summary>
        /// 
        /// </summary>
        public const int HowOftenToReportStatusInMilliseconds = 30000;

        /// <summary>
        /// Maximum links that can be output
        /// </summary>
        public const int MaximumLinkQuota = 60000;

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
                    while (reader.Peek() >= 0)
                    {
                        string line = reader.ReadLine();
                        var item = QueueItem.Parse(line);

                        while (!await taskPool.WaitAsync(30000).ConfigureAwait(false))
                        {
                            //log.Info("Waiting to ValidateQueueItem");
                            if (QuotaReached())
                                break;
                        }
                        var t = ValidateQueueItem(item).ConfigureAwait(false);
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

        void ReportStatus() => log.InfoFormat("NEXTLINKS: Active Fetches {0}", _activeTasks);

        /// <summary>
        /// Asyncronously validates a QueueItem
        /// </summary>
        /// <param name="item">The queue item</param>
        /// <returns>Async task (no return value)</returns>
        async Task ValidateQueueItem(QueueItem item)
        {
            try
            {
                Interlocked.Increment(ref _activeTasks);

                if (item == null)
                {
                    // item has an issue

                }

                else if (QuotaReached())
                {
                    RejectLink(item);
                }

                else if (item.HasAnIssue)
                {
                    RejectLink(item);
                }

                else if (IsMalformedQueueItem(item))
                {
                    item.MalformedUrl = true;
                    RejectLink(item);
                }

                else if (CantDownloadItYet(item))
                {
                    item.UnsupportedUri = true;
                    RejectLink(item);
                }

                else if (IsDomainInALanguageICantRead(item))
                {
                    item.IsBlockedByDomain = true;
                    RejectLink(item);
                }

                else if (IsUriProbablyBlocked(item))
                {
                    item.IsProbablyBlocked = true;
                    RejectLink(item);
                }

                else if (IsSequenceTooHigh(item))
                {
                    item.PriorityTooLow = true;
                    RejectLink(item);
                }

                else if (await IsBlockedByRobots(item)) // most expensive one last
                {
                    item.BlockedByRobots = true;
                    RejectLink(item);
                }

                else
                {
                    AcceptLink(item);
                }
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

        void AcceptLink(QueueItem item)
        {
            //log.InfoFormat("AcceptLink {0}", item.TargetUri);
            OutputAcceptedLink(item);
        }

        void RejectLink(QueueItem item)
        {
            //log.InfoFormat("RejectLink {0}", item.TargetUri);
            OutputRejectedLink(item);
        }

        void OutputAcceptedLink(QueueItem item)
        {
            LinksAccepted++;
            lock(acceptStreamLock)
                acceptStream?.WriteLine(item.ToString());
        }

        void OutputRejectedLink(QueueItem item)
        {
            LinksRejected++;
            lock(rejectStreamLock)
                rejectStream?.WriteLine(item.ToString());
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
        bool IsSequenceTooHigh(QueueItem item) => item.Priority > MaximumSequenceForLinks;

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
        /// <returns></returns>
        bool IsUriProbablyBlocked(QueueItem item) =>
            item == null ||
            item.TargetUri == null ||
            item.TargetUri.AbsolutePath.ToString().EndsWith(".jpg") ||
            item.TargetUri.AbsolutePath.ToString().EndsWith(".jpeg") ||
            item.TargetUri.AbsolutePath.ToString().EndsWith(".gif") ||
            item.TargetUri.AbsolutePath.ToString().EndsWith(".png") ||
            item.TargetUri.AbsolutePath.ToString().EndsWith(".ico") ||
            item.TargetUri.AbsolutePath.ToString().EndsWith(".svg") ||
            item.TargetUri.AbsolutePath.ToString().EndsWith(".avi") ||
            item.TargetUri.AbsolutePath.ToString().EndsWith(".mp4") ||
            item.TargetUri.AbsolutePath.ToString().EndsWith(".mp3") ||
            item.TargetUri.AbsolutePath.ToString().EndsWith(".wav");

        /// <summary>
        /// Block things I can't read
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
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
            item.TargetUri.Host.EndsWith(".br");

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
                var watch = new Stopwatch();
                watch.Start();
                var r = await HostCacheManager.GetRobotsFile(item.TargetUri.Host);
                if (r == null) rtn = false;
                else if (r.IsDisallowed(item.TargetUri)) rtn = true;
                watch.Stop();
                if (watch.ElapsedMilliseconds > 1000) log.InfoFormat("IsBlockedByRobots took {0}ms", watch.ElapsedMilliseconds);
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

