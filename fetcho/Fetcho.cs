using System;
using System.Collections.Generic;
using System.IO;
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
        int completedFetches = 0;
        int waitingForFetchTimeout = 0;
        SemaphoreSlim fetchLock = null;
        PressureReliefValve<QueueItem> valve = null;
        Random random = new Random(DateTime.UtcNow.Millisecond);
        DateTime startTime = DateTime.UtcNow;

        private ISourceBlock<IEnumerable<QueueItem>> FetchQueueIn;
        private ITargetBlock<IEnumerable<QueueItem>> RequeueOut;
        private BufferBlock<WebDataPacketWriter> DataWritersPool;

        public bool Running { get; set; }

        public Fetcho(
            ISourceBlock<IEnumerable<QueueItem>> fetchQueueIn,
            ITargetBlock<IEnumerable<QueueItem>> requeueOut)
        {
            Running = true;
            valve = CreatePressureReliefValve();
            FetchQueueIn = fetchQueueIn;
            RequeueOut = requeueOut;
            fetchLock = new SemaphoreSlim(FetchoConfiguration.Current.MaxConcurrentFetches);
            FetchoConfiguration.Current.ConfigurationChange += (sender, e) => UpdateConfigurationSettings(e);

        }

        public async Task Process()
        {
            try
            {
                log.Info("Fetcho.Process() commenced");
                RunPreStartChecks();
                await CreateDataPacketWriters();

                var r = ReportStatus();

                log.Info("Fetcho.Process() queue items started");
                var u = await NextQueueItem();

                while (Running)
                {
                    while (!await fetchLock.WaitAsync(FetchoConfiguration.Current.TaskStartupWaitTimeInMilliseconds))
                        LogStatus("Waiting for a fetchLock");

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
                CloseAllWriters();
                log.Info("Fetcho.Process() complete");
            }
        }


        private async Task ReportStatus()
        {
            try
            {
                while (true)
                {
                    await Task.Delay(FetchoConfiguration.Current.HowOftenToReportStatusInMilliseconds);
                    LogStatus("STATUS UPDATE");
                }

            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
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
                foreach (var item in items)
                {
                    await FetchQueueItem(item);
                    Interlocked.Increment(ref waitingForFetchTimeout);
                    await Task.Delay(FetchoConfiguration.Current.MaxFetchSpeedInMilliseconds + 10);
                    Interlocked.Decrement(ref waitingForFetchTimeout);
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

        private async Task FetchQueueItem(QueueItem item)
        {
            try
            {
                if (!await valve.WaitToEnter(item))
                {
                    LogStatus(String.Format("IP congested, waited too long for access to {0}", item.TargetUri));
                    await SendItemsForRequeuing(new QueueItem[] { item });
                }
                else
                {
                    try
                    {
                        await ResourceFetcher.FetchFactory(
                            item.TargetUri,
                            item.SourceUri,
                            DateTime.MinValue, // TODO: Get this from the DB
                            DataWritersPool
                            );

                        var writer = await DataWritersPool.ReceiveAsync().ConfigureAwait(false);
                        writer = ReplaceDataPacketWriterIfQuotaReached(writer);
                        await DataWritersPool.SendAsync(writer);
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

        /// <summary>
        /// Output the item for requeuing
        /// </summary>
        /// <param name="item"></param>
        private async Task SendItemsForRequeuing(IEnumerable<QueueItem> items)
        {
            await RequeueOut.SendAsync(items);
        }

        private async Task CreateDataPacketWriters()
        {
            DataWritersPool = new BufferBlock<WebDataPacketWriter>();
            var packet = CreateNewDataPacketWriter();
            await DataWritersPool.SendAsync(packet);
        }

        private WebDataPacketWriter CreateNewDataPacketWriter()
        {
            if (String.IsNullOrWhiteSpace(FetchoConfiguration.Current.DataSourcePath))
                throw new FetchoException("No output data file is set");

            string fileName = Path.Combine(FetchoConfiguration.Current.DataSourcePath, "packet.xml");

            return new WebDataPacketWriter(fileName);
        }

        private WebDataPacketWriter ReplaceDataPacketWriterIfQuotaReached(WebDataPacketWriter writer)
        {
            if (writer.ResourcesWritten > FetchoConfiguration.Current.MaxResourcesPerDataPacket)
                return ReplaceDataPacketWriter(writer);
            return writer;
        }

        private WebDataPacketWriter ReplaceDataPacketWriter(WebDataPacketWriter writer)
        {
            writer.Dispose();
            return CreateNewDataPacketWriter();
        }

        private void CloseAllWriters()
        {
            while (DataWritersPool.Count > 0)
            {
                var packet = DataWritersPool.Receive();
                packet.Dispose();
            }
        }

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

        private async Task<IPAddress> GetQueueItemTargetIP(QueueItem item) =>
            item.TargetIP != null && !item.TargetIP.Equals(IPAddress.None) ? item.TargetIP : await Utility.GetHostIPAddress(item.TargetUri);

        private void LogStatus(string status) =>
            log.InfoFormat("{0}: In Valve {1}, Fetching {2}, Waiting(IP) {3}, Waiting(Timeout) {4}, Waiting(Write): {5}, completed {6}, active chunks {7}, running {8}",
                            status,
                            valve.TasksInValve,
                            ResourceFetcher.ActiveFetches,
                            valve.TasksWaiting,
                            waitingForFetchTimeout,
                            ResourceFetcher.WaitingToWrite,
                            completedFetches,
                            FetchoConfiguration.Current.MaxConcurrentFetches - fetchLock.CurrentCount,
                            (DateTime.UtcNow - startTime));

    }
}

