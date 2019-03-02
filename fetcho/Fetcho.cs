using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;
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
        public FetchoConfiguration Configuration { get; set; }
        SemaphoreSlim fetchLock = null;
        PressureReliefValve<QueueItem> valve = null;
        Random random = new Random(DateTime.Now.Millisecond);
        Stopwatch spoolingTimeWatch = new Stopwatch();
        TimeSpan lastSpoolingTime;
        DateTime startTime = DateTime.Now;

        private BufferBlock<QueueItem> FetchQueueIn;
        private BufferBlock<QueueItem> RequeueOut;
        private BufferBlock<WebDataPacketWriter> DataWritersCycle;

        public bool Running { get; set; }

        public Fetcho(FetchoConfiguration config, BufferBlock<QueueItem> fetchQueueIn, BufferBlock<QueueItem> requeueOut)
        {
            Running = true;
            Configuration = config;
            valve = CreatePressureReliefValve();
            FetchQueueIn = fetchQueueIn;
            RequeueOut = requeueOut;
            fetchLock = new SemaphoreSlim(Configuration.MaxConcurrentFetches);
        }

        public async Task Process()
        {
            try
            {
                RunPreStartChecks();
                log.Info("Fetcho.Process() commenced");
                await CreateDataPacketWriters();

                var r = ReportStatus();

                var u = await NextQueueItem();

                while (Running)
                {
                    spoolingTimeWatch.Start();
                    while (!await fetchLock.WaitAsync(Configuration.TaskStartupWaitTimeInMilliseconds))
                        LogStatus("Waiting for a fetchLock");

                    u = await CreateTaskToCrawlIPAddress(u);
                    spoolingTimeWatch.Stop();
                    lastSpoolingTime = spoolingTimeWatch.Elapsed;
                    spoolingTimeWatch.Reset();
                }

                // wait for all the tasks to finish before shutting down
                while (fetchLock.CurrentCount < Configuration.MaxConcurrentFetches)
                    await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                await CloseAllWriters();
                log.Info("Fetcho.Process() complete");
            }
        }


        private async Task ReportStatus()
        {
            while (true)
            {
                await Task.Delay(Configuration.HowOftenToReportStatusInMilliseconds);
                LogStatus("STATUS UPDATE");

                //if (fetchLock.CurrentCount < MaxConcurrentFetches)
                //    return;
            }
        }

        /// <summary>
        /// Get the next item off the queue
        /// </summary>
        /// <returns></returns>
        private async Task<QueueItem> NextQueueItem() => await FetchQueueIn.ReceiveAsync();

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
                    await Task.Delay(Settings.MaximumFetchSpeedMilliseconds + 10);
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
                    await SendItemForRequeuing(item);
                }
                else
                {
                    try
                    {
                        await ResourceFetcher.FetchFactory(
                            item.SourceUri,
                            item.TargetUri,
                            DataWritersCycle,
                            Configuration.BlockProvider,
                            DateTime.MinValue
                            );

                        var writer = await DataWritersCycle.ReceiveAsync();
                        writer = ReplaceDataPacketWriterIfQuotaReached(writer);
                        await DataWritersCycle.SendAsync(writer);
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
                log.Error(ex);
            }
        }

        /// <summary>
        /// Output the item for requeuing
        /// </summary>
        /// <param name="item"></param>
        private async Task SendItemForRequeuing(QueueItem item)
        {
            await RequeueOut.SendAsync(item);
        }

        private async Task CreateDataPacketWriters()
        {
            DataWritersCycle = new BufferBlock<WebDataPacketWriter>();
            var packet = CreateNewDataPacketWriter();
            await DataWritersCycle.SendAsync(packet);
        }

        private WebDataPacketWriter CreateNewDataPacketWriter()
        {
            if (String.IsNullOrWhiteSpace(Configuration.DataSourcePath))
                throw new FetchoException("No output data file is set");

            string fileName = Path.Combine(Configuration.DataSourcePath, "packet.xml");

            return new WebDataPacketWriter(fileName);
        }

        private WebDataPacketWriter ReplaceDataPacketWriterIfQuotaReached(WebDataPacketWriter writer)
        {
            if (writer.ResourcesWritten > Configuration.MaximumResourcesPerDataPacket)
                return ReplaceDataPacketWriter(writer);
            return writer;
        }

        private WebDataPacketWriter ReplaceDataPacketWriter(WebDataPacketWriter writer)
        {
            writer.Dispose();
            return CreateNewDataPacketWriter();
        }

        private async Task CloseAllWriters()
        {
            while (DataWritersCycle.Count > 0)
            {
                var packet = await DataWritersCycle.ReceiveAsync();
                packet.Dispose();
            }
        }

        private void RunPreStartChecks()
        {
            // not sure why it thinks this - probably comparing ints rather than longs
            if (Settings.MaxFileDownloadLengthInBytes * (long)Configuration.MaxConcurrentFetches > (long)4096 * 1024 * 1024)
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
            var prv = new PressureReliefValve<QueueItem>(Configuration.PressureReliefThreshold);

            prv.WaitFunc = async (item) =>
                await HostCacheManager.WaitToFetch(
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
                Configuration.MinPressureReliefValveWaitTimeInMilliseconds,
                Configuration.MaxPressureReliefValveWaitTimeInMilliseconds
                );

        private async Task<IPAddress> GetQueueItemTargetIP(QueueItem item) =>
            item.TargetIP != null && !item.TargetIP.Equals(IPAddress.None) ? item.TargetIP : await Utility.GetHostIPAddress(item.TargetUri);

        private void LogStatus(string status) =>
            log.InfoFormat("{0}: In Valve {1}, Fetching {2}, Waiting for IP {3}, Waiting For Fetch Timeout {4}, Waiting to Write: {5}, Completed {6}, Spooling time {7}, Active Chunks {8}, Running Time {9}",
                            status,
                            valve.TasksInValve,
                            ResourceFetcher.ActiveFetches,
                            valve.TasksWaiting,
                            waitingForFetchTimeout,
                            ResourceFetcher.WaitingToWrite,
                            completedFetches,
                            lastSpoolingTime,
                            Configuration.MaxConcurrentFetches - fetchLock.CurrentCount,
                            (DateTime.Now - startTime));

    }
}

