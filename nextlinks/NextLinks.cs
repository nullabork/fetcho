﻿
using System;
using System.Collections.Generic;
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
        public const int MaximumSequenceForLinks = 200000000;

        /// <summary>
        /// 
        /// </summary>
        public const int HowOftenToReportStatusInMilliseconds = 30000;

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

        public TextWriter acceptStream;
        public TextWriter rejectStream;

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
        public NextLinks(NextLinksConfiguration config)
        {
            Configuration = config;
        }

        public async Task Process()
        {

            try
            {
                var cts = new CancellationTokenSource();
                var cancellationToken = cts.Token;

                acceptStream = GetAcceptStream();
                rejectStream = GetRejectStream();

                using (var reader = GetInputStream())
                {
                    while (reader.Peek() >= 0)
                    {
                        string line = reader.ReadLine();
                        var item = QueueItem.Parse(line);
                        var t = ValidateQueueItem(item, cancellationToken);
                    }
                }

                while (true)
                {
                    await Task.Delay(HowOftenToReportStatusInMilliseconds, cancellationToken);
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
        async Task ValidateQueueItem(QueueItem item, CancellationToken cancellationToken)
        {
            try
            {
                Interlocked.Increment(ref _activeTasks);
                while (!await taskPool.WaitAsync(10000, cancellationToken))
                {
                    log.Info("Waiting to ValidateQueueItem");
                    if (QuotaReached())
                        break;
                }

                if (item == null)
                {
                    // item has an issue

                }

                else if (QuotaReached())
                {
                    await RejectLink(item);
                }

                else if (item.HasAnIssue)
                {
                    await RejectLink(item);
                }

                else if (IsMalformedQueueItem(item))
                {
                    item.MalformedUrl = true;
                    await RejectLink(item);
                }

                else if (IsSequenceTooHigh(item))
                {
                    item.SequenceTooHigh = true;
                    await RejectLink(item);
                }

                else if (await IsBlockedByRobots(item, cancellationToken))
                {
                    item.BlockedByRobots = true;
                    await RejectLink(item);
                }

                else
                {
                    await AcceptLink(item);
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
            log.InfoFormat("AcceptLink {0}", item.TargetUri);
            OutputAcceptedLink(item);
        }

        void RejectLink(QueueItem item)
        {
            log.InfoFormat("RejectLink {0}", item.TargetUri);
            OutputRejectedLink(item);
        }

        void OutputAcceptedLink(QueueItem item)
        {
            LinksAccepted++;
            if (acceptStream != null)
                acceptStream.WriteLine(item.ToString());
        }

        void OutputRejectedLink(QueueItem item)
        {
            LinksRejected++;
            if (rejectStream != null)
                rejectStream.WriteLine(item.ToString());
        }

        TextWriter GetRejectStream()
        {
            if (String.IsNullOrWhiteSpace(Configuration.RejectedLinkFilePath))
                return null;

            return new StreamWriter(Configuration.RejectedLinkFilePath, false);
        }

        TextWriter GetAcceptStream()
        {
            return Console.Out;
        }

        /// <summary>
        /// Returns true if we've reached the maximum accepted links
        /// </summary>
        /// <returns></returns>
        bool QuotaReached() => QuotaEnabled && LinksAccepted >= MaxConcurrentTasks;

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
        bool IsSequenceTooHigh(QueueItem item) => item.Sequence > MaximumSequenceForLinks;

        /// <summary>
        /// Returns true if the queue item is blocked by a rule in the associated robots file
        /// </summary>
        /// <param name="item"></param>
        /// <returns>bool</returns>
        async Task<bool> IsBlockedByRobots(QueueItem item, CancellationToken cancellationToken)
        {
            bool rtn = false;

            try
            {
                var watch = new Stopwatch();
                watch.Start();
                var r = await HostCacheManager.GetRobotsFile(item.TargetUri.Host, cancellationToken);
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
