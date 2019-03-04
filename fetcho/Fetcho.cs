using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        DateTime startTime = DateTime.Now;

        private BufferBlock<IEnumerable<QueueItem>> FetchQueueIn;
        private BufferBlock<IEnumerable<QueueItem>> RequeueOut;
        private BufferBlock<WebDataPacketWriter> DataWritersCycle;
        private BufferBlock<Database> DatabasePool;

        public bool Running { get; set; }

        public Fetcho(FetchoConfiguration config, BufferBlock<IEnumerable<QueueItem>> fetchQueueIn, BufferBlock<IEnumerable<QueueItem>> requeueOut)
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
                log.Info("Fetcho.Process() commenced");
                RunPreStartChecks();
                await CreateDataPacketWriters();
                await CreateDatabasePool();

                var r = ReportStatus();

                log.Info("Fetcho.Process() queue items started");
                var u = await NextQueueItem();

                while (Running)
                {
                    while (!await fetchLock.WaitAsync(Configuration.TaskStartupWaitTimeInMilliseconds))
                        LogStatus("Waiting for a fetchLock");

                    u = await CreateTaskToCrawlIPAddress(u);
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
                CloseAllWriters();
                CloseAllDatabases();
                log.Info("Fetcho.Process() complete");
            }
        }


        private async Task ReportStatus()
        {
            try
            {
                while (true)
                {
                    await Task.Delay(Configuration.HowOftenToReportStatusInMilliseconds);
                    LogStatus("STATUS UPDATE");
                }

            }
            catch(Exception ex)
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
            while ( queueItemEnum == null || !queueItemEnum.MoveNext())
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
                    await SendItemsForRequeuing(new QueueItem[] { item });
                }
                else
                {
                    try
                    {
                        await ResourceFetcher.FetchFactory(
                            item.SourceUri,
                            item.TargetUri,
                            DataWritersCycle,
                            DatabasePool,
                            Configuration.BlockProvider,
                            DateTime.MinValue
                            );

                        var writer = await DataWritersCycle.ReceiveAsync().ConfigureAwait(false);
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
        private async Task SendItemsForRequeuing(IEnumerable<QueueItem> items)
        {
            await RequeueOut.SendAsync(items);
        }

        private async Task CreateDataPacketWriters()
        {
            DataWritersCycle = new BufferBlock<WebDataPacketWriter>();
            var packet = CreateNewDataPacketWriter();
            await DataWritersCycle.SendAsync(packet);
        }

        private async Task CreateDatabasePool()
        {
            DatabasePool = new BufferBlock<Database>();
            for ( int i=0;i<Configuration.DatabasePoolSize;i++)
            {
                var db = new Database();
                await db.Open();
                DatabasePool.Post(db);
            }
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

        private void CloseAllWriters()
        {
            while (DataWritersCycle.Count > 0)
            {
                var packet = DataWritersCycle.Receive();
                packet.Dispose();
            }
        }

        private void CloseAllDatabases()
        {
            while (DatabasePool.Count > 0)
            {
                var db = DatabasePool.Receive();
                db.Dispose();
                db = null;
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
            log.InfoFormat("{0}: In Valve {1}, Fetching {2}, Waiting(IP) {3}, Waiting(Timeout) {4}, Waiting(Write): {5}, completed {6}, active chunks {7}, running {8}",
                            status,
                            valve.TasksInValve,
                            ResourceFetcher.ActiveFetches,
                            valve.TasksWaiting,
                            waitingForFetchTimeout,
                            ResourceFetcher.WaitingToWrite,
                            completedFetches,
                            Configuration.MaxConcurrentFetches - fetchLock.CurrentCount,
                            (DateTime.Now - startTime));

    }
}

