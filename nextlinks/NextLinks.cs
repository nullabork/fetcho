
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
        public const int InitialBufferSize = 10000;

        /// <summary>
        /// Queue items with a number higher than this will be rejected 
        /// </summary>
        public const int MaximumSequenceForLinks = 200000000;

        /// <summary>
        /// Log4Net logger
        /// </summary>
        static readonly ILog log = LogManager.GetLogger(typeof(NextLinks));

        /// <summary>
        /// Links to accept
        /// </summary>
        List<QueueItem> links = new List<QueueItem>(InitialBufferSize);

        /// <summary>
        /// Links to reject
        /// </summary>
        List<QueueItem> rejects = new List<QueueItem>(InitialBufferSize);

        /// <summary>
        /// Syncronise the access to links & rejects using this
        /// </summary>
        readonly object collectionLocker = new object();

        /// <summary>
        /// I'm the real SemaphoreSlim ... shady
        /// </summary>
        /// <remarks>Limits the number of queue validations occurring at once to the initial buffer size to avoid downloading more robots than we need to</remarks>
        static readonly SemaphoreSlim maxNextLinks = new SemaphoreSlim(InitialBufferSize);

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
                var tasks = new List<Task>(InitialBufferSize);

                using (var reader = GetInputStream())
                {
                    while (reader.Peek() >= 0)
                    {
                        string line = reader.ReadLine();
                        var item = QueueItem.Parse(line);

                        if (tasks.Count % 100 == 0) Thread.Sleep(1);

                        tasks.Add(ValidateQueueItem(item));
                    }
                }

                Task.WaitAll(tasks.ToArray());

                OutputLinks();
                WriteOutRejects();
            }
            catch (Exception ex)
            {
                log.Error("NextLinks", ex);
            }
        }

        /// <summary>
        /// Asyncronously validates a QueueItem
        /// </summary>
        /// <param name="item">The queue item</param>
        /// <returns>Async task (no return value)</returns>
        async Task ValidateQueueItem(QueueItem item)
        {
            try
            {
                while (!await maxNextLinks.WaitAsync(10000))
                    if (QuotaReached())
                        break;

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

                else if (IsSequenceTooHigh(item))
                {
                    item.SequenceTooHigh = true;
                    RejectLink(item);
                }

                else if (await IsBlockedByRobots(item))
                {
                    item.BlockedByRobots = true;
                    RejectLink(item);
                }

                else
                {
                    AcceptLink(item);
                }
            }
            catch( Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                maxNextLinks.Release();
            }
        }

        void AcceptLink(QueueItem item) { lock (collectionLocker) links.Add(item); }
        void RejectLink(QueueItem item) { lock (collectionLocker) rejects.Add(item); }

        void OutputLinks()
        {
            foreach (var item in links)
            {
                Console.WriteLine(item);
            }
        }

        void WriteOutRejects()
        {
            // bail if there's no where to write them
            if (String.IsNullOrWhiteSpace(Configuration.RejectedLinkFilePath))
                return;

            using (var stream = new StreamWriter(Configuration.RejectedLinkFilePath, false))
            {
                foreach (var item in rejects)
                {
                    stream.WriteLine(item);
                }
            }
        }

        /// <summary>
        /// Returns true if we've reached the maximum accepted links
        /// </summary>
        /// <returns></returns>
        bool QuotaReached() => links.Count >= InitialBufferSize;

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

