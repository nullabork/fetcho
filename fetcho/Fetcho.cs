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
    /// Fetches URIs from the input stream
    /// </summary>
    public class Fetcho
    {
        static readonly ILog log = LogManager.GetLogger(typeof(Fetcho));
        SemaphoreSlim fetchLock = null;
        PressureReliefValve<QueueItem> valve = null;
        Random random = new Random(DateTime.UtcNow.Millisecond);

        private ISourceBlock<IEnumerable<QueueItem>> FetchQueueIn;
        private ITargetBlock<IEnumerable<QueueItem>> RequeueOut;
        private BufferBlock<IWebResourceWriter> DataWritersPool;
        private ITargetBlock<QueueItem> VisitedURIsRecorder;

        public bool Running { get; set; }

        public int CompletedFetches { get => completedFetches; }
        private int completedFetches = 0;

        public TimeSpan Uptime { get => DateTime.UtcNow - startTime; }
        private DateTime startTime = DateTime.UtcNow;

        public int WaitingForFetchTimeout { get => waitingForFetchTimeout; }
        private int waitingForFetchTimeout = 0;

        public int WaitingFromIPCongestion { get => valve.TasksWaiting; }

        public int ActiveChunkCount { get => FetchoConfiguration.Current.MaxConcurrentFetches - fetchLock.CurrentCount; }

        public int TotalPagesPerMinute { get => (int)Uptime.TotalMinutes == 0 ? 0 : CompletedFetches / (int)Uptime.TotalMinutes; }

        public Fetcho(
            ISourceBlock<IEnumerable<QueueItem>> fetchQueueIn,
            ITargetBlock<IEnumerable<QueueItem>> requeueOut,
            BufferBlock<IWebResourceWriter> dataWritersPool)
        {
            Running = true;
            valve = CreatePressureReliefValve();
            FetchQueueIn = fetchQueueIn;
            RequeueOut = requeueOut;
            DataWritersPool = dataWritersPool;
            fetchLock = new SemaphoreSlim(FetchoConfiguration.Current.MaxConcurrentFetches);
            FetchoConfiguration.Current.ConfigurationChange += (sender, e) => UpdateConfigurationSettings(e);
        }

        public void Shutdown() => Running = false;

        public async Task Process()
        {
            try
            {
                log.Info("Fetcho.Process() commenced");
                RunPreStartChecks();

                VisitedURIsRecorder = await CreateBlockToRecordVisitedURIs();

                log.Info("Fetcho.Process() queue items started");
                var u = await NextQueueItem();

                while (Running)
                {
                    while (!await fetchLock.WaitAsync(FetchoConfiguration.Current.TaskStartupWaitTimeInMilliseconds))
                        log.Debug("Waiting for a fetchLock");

                    u = await CreateTaskToCrawlIPAddress(u);
                }

                // wait for all the tasks to finish before shutting down
                while (fetchLock.CurrentCount < FetchoConfiguration.Current.MaxConcurrentFetches)
                    await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                log.Info("Fetcho.Process() complete");
            }
        }

        /// <summary>
        /// Get the next item off the queue
        /// </summary>
        /// <returns></returns>
        private async Task<QueueItem> NextQueueItem()
        {
            while (queueItemEnum == null || !queueItemEnum.MoveNext())
            {
                queueItemEnum = (await FetchQueueIn.ReceiveAsync().ConfigureAwait(false)).GetEnumerator();
            }

            return queueItemEnum.Current;
        }
        private IEnumerator<QueueItem> queueItemEnum = null;

        private async Task<QueueItem> CreateTaskToCrawlIPAddress(QueueItem item)
        {
            var addr = await GetQueueItemTargetIP(item);
            var nextaddr = addr;

            var l = new List<QueueItem>(10);

            while (addr.Equals(nextaddr))
            {
                l.Add(item);
                item = await NextQueueItem();

                if (item == null)
                    nextaddr = IPAddress.None;
                else
                    nextaddr = await GetQueueItemTargetIP(item);
            }

            var t = FetchChunkOfQueueItems(addr, l);

            return item;
        }

        /// <summary>
        /// Fetch several queue items in series with a wait between each
        /// </summary>
        /// <param name="hostaddr"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        private async Task FetchChunkOfQueueItems(IPAddress hostaddr, IEnumerable<QueueItem> items)
        {
            try
            {
                DateTime startTime = DateTime.Now;

                foreach (var item in items)
                {
                    if (!Running) break; // we're shutting down
                    await FetchQueueItem(item, startTime);
                    startTime = DateTime.Now; // update to start timing for the next one

                    if (await FetchoConfiguration.Current.HostCache.HasHostExceedNetworkIssuesThreshold(item.TargetIP))
                    {
                        Utility.LogInfo("Too many network issues connecting to {0}, dropping the whole chunk of {1} items", hostaddr, items.Count());
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                log.ErrorFormat("FetchChunkOfQueueItems: {0}", ex);
            }
            finally
            {
                fetchLock.Release();
            }

        }

        private async Task FetchQueueItem(QueueItem item, DateTime startTime)
        {
            try
            {
                if (!await valve.WaitToEnter(item, item.CanBeDiscarded))
                {
                    log.Error(String.Format("IP congested, waited too long for access to {0}", item.TargetUri));
                    await SendItemsForRequeuing(new QueueItem[] { item });
                }
                else
                {
                    // will wait a bit longer if we're too quick
                    await WaitForFetchTimeout(startTime);

                    try
                    {
                        await VisitedURIsRecorder.SendOrWaitAsync(item).ConfigureAwait(false);
                        await ResourceFetcher.FetchFactory(
                            item,
                            item.TargetUri,
                            item.SourceUri,
                            DateTime.MinValue, // TODO: Get this from the DB
                            DataWritersPool
                            );

                        item.Status = QueueItemStatus.Fetched;
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    finally
                    {
                        Interlocked.Increment(ref completedFetches);
                        valve.Exit(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
        }

        private async Task WaitForFetchTimeout(DateTime startTime)
        {
            int waitTime = (int)(DateTime.Now - startTime).TotalMilliseconds - (FetchoConfiguration.Current.MaxFetchSpeedInMilliseconds + 10);
            if (waitTime > 0)
            {
                Interlocked.Increment(ref waitingForFetchTimeout);
                await Task.Delay(waitTime);
                Interlocked.Decrement(ref waitingForFetchTimeout);
            }
        }

        /// <summary>
        /// Output the item for requeuing
        /// </summary>
        /// <param name="item"></param>
        private async Task SendItemsForRequeuing(IEnumerable<QueueItem> items)
            => await RequeueOut.SendAsync(items).ConfigureAwait(false);

        private void RunPreStartChecks()
        {
            // not sure why it thinks this - probably comparing ints rather than longs
            if (FetchoConfiguration.Current.MaxFileDownloadLengthInBytes * (long)FetchoConfiguration.Current.MaxConcurrentFetches > (long)4096 * 1024 * 1024)
#pragma warning disable CS0162 // Unreachable code detected
                log.WarnFormat("MaxConcurrentFetches * MaxFileDownloadLengthInBytes is greater than 4GB. For safety this should not be this big");
#pragma warning restore CS0162 // Unreachable code detected
        }

        /// <summary>
        /// Creates the pressure relief valve to throw out tasks
        /// </summary>
        /// <returns></returns>
        private PressureReliefValve<QueueItem> CreatePressureReliefValve()
        {
            var prv = new PressureReliefValve<QueueItem>(FetchoConfiguration.Current.PressureReliefThreshold);

            prv.WaitFunc = async (item) =>
                await FetchoConfiguration.Current.HostCache.WaitToFetch(
                        await GetQueueItemTargetIP(item),
                        GetRandomReliefTimeout() // randomised to spread the timeouts evenly to avoid bumps
                    );

            return prv;
        }

        /// <summary>
        /// Relief timeout random
        /// </summary>
        /// <returns></returns>
        private int GetRandomReliefTimeout() =>
            random.Next(
                FetchoConfiguration.Current.MinPressureReliefValveWaitTimeInMilliseconds,
                FetchoConfiguration.Current.MaxPressureReliefValveWaitTimeInMilliseconds
                );

        private void UpdateConfigurationSettings(ConfigurationChangeEventArgs e)
        {
            e.IfPropertyIs(
                 () => FetchoConfiguration.Current.MaxConcurrentFetches,
                 () => UpdateFetchLockConfiguration(e)
            );

            e.IfPropertyIs(
                 () => FetchoConfiguration.Current.PressureReliefThreshold,
                 () => UpdateValveConfiguration(e)
            );
        }

        private void UpdateFetchLockConfiguration(ConfigurationChangeEventArgs e)
            => fetchLock.ReleaseOrReduce((int)e.OldValue, (int)e.NewValue).GetAwaiter().GetResult();

        private void UpdateValveConfiguration(ConfigurationChangeEventArgs e)
            => valve.WaitingThreshold = (int)e.NewValue;

        private async Task<IPAddress> GetQueueItemTargetIP(QueueItem item)
            => item.TargetIP != null && !item.TargetIP.Equals(IPAddress.None) ? item.TargetIP : await Utility.GetHostIPAddress(item.TargetUri);

        private async Task<ITargetBlock<QueueItem>> CreateBlockToRecordVisitedURIs()
        {
            var db = await DatabasePool.GetDatabaseAsync().ConfigureAwait(false);
            var block = new ActionBlock<QueueItem>(async item =>
                    await db.SaveWebResource(item.TargetUri, DateTime.UtcNow.AddDays(FetchoConfiguration.Current.PageCacheExpiryInDays))
                );
            return block;
        }
    }
}

