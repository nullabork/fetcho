using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
        const int MaxConcurrentFetches = 2000;
        const int PressureReliefThreshold = MaxConcurrentFetches * 5 / 10; // if it totally fills up it'll chuck some out
        const int HowOftenToReportStatusInMilliseconds = 30000;
        const int TaskStartupWaitTimeInMilliseconds = 360000;
        const int MinPressureReliefValveWaitTimeInMilliseconds = Settings.MaximumFetchSpeedMilliseconds * 2;
        const int MaxPressureReliefValveWaitTimeInMilliseconds = Settings.MaximumFetchSpeedMilliseconds * 12;

        static readonly ILog log = LogManager.GetLogger(typeof(Fetcho));
        int completedFetches = 0;
        int waitingForFetchTimeout = 0;
        public FetchoConfiguration Configuration { get; set; }
        SemaphoreSlim fetchLock = new SemaphoreSlim(MaxConcurrentFetches);
        readonly object requeueWriterLock = new object();
        TextWriter requeueWriter = null;
        XmlWriter outputWriter = null;
        TextReader inputReader = null;
        PressureReliefValve<QueueItem> valve = null;
        Random random = new Random(DateTime.Now.Millisecond);
        Stopwatch spoolingTimeWatch = new Stopwatch();
        TimeSpan lastSpoolingTime;

        public Fetcho(FetchoConfiguration config)
        {
            Configuration = config;
            valve = CreatePressureReliefValve();
        }

        public async Task Process()
        {
            try
            {
                RunPreStartChecks();
                log.Info("Fetcho.Process() commenced");
                requeueWriter = GetRequeueWriter();
                inputReader = GetInputReader();

                var r = ReportStatus();
                OpenNewOutputWriter();

                var u = NextQueueItem();

                while (u != null)
                {
                    spoolingTimeWatch.Start();
                    while (!await fetchLock.WaitAsync(TaskStartupWaitTimeInMilliseconds))
                        LogStatus("Waiting for a fetchLock");

                    u = await CreateTaskToCrawlIPAddress(u);
                    spoolingTimeWatch.Stop();
                    lastSpoolingTime = spoolingTimeWatch.Elapsed;
                    spoolingTimeWatch.Reset();
                }

                // wait for all the tasks to finish before shutting down
                while (fetchLock.CurrentCount < MaxConcurrentFetches)
                    await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {

                CloseOutputWriter();
                CloseRequeueWriter();
                log.Info("Fetcho.Process() complete");
            }
        }


        private async Task ReportStatus()
        {
            while (true)
            {
                await Task.Delay(HowOftenToReportStatusInMilliseconds);
                LogStatus("STATUS UPDATE");

                if (valve.TasksInValve <= 0)
                    return;
            }
        }

        private QueueItem ParseQueueItem(string line)
        {
            try
            {
                if (Configuration.InputRawUrls)
                    return new QueueItem() { TargetUri = new Uri(line), Priority = 0 };
                else
                    return QueueItem.Parse(line);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Get the next item off the queue
        /// </summary>
        /// <returns></returns>
        private QueueItem NextQueueItem(int retriesLeft = 10)
        {
            if (retriesLeft == 0) return null;
            var line = inputReader.ReadLine();

            if (String.IsNullOrWhiteSpace(line))
                return null;

            var item = ParseQueueItem(line);

            if (item == null || item.HasAnIssue)
            {
                log.InfoFormat("QueueItem has an issue:{0}", item);
                return NextQueueItem(--retriesLeft);
            }
            else
                return item;
        }

        private async Task<QueueItem> CreateTaskToCrawlIPAddress(QueueItem item)
        {
            var addr = await GetQueueItemTargetIP(item);
            var nextaddr = addr;

            var l = new List<QueueItem>(10);

            while (addr.Equals(nextaddr))
            {
                l.Add(item);
                item = NextQueueItem();

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
                    OutputItemForRequeuing(item);
                }
                else
                {
                    try
                    {
                        await ResourceFetcher.FetchFactory(
                            item.SourceUri,
                            item.TargetUri,
                            outputWriter,
                            Configuration.BlockProvider,
                            DateTime.MinValue
                            );
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
        private void OutputItemForRequeuing(QueueItem item)
        {
            lock (requeueWriterLock) requeueWriter?.WriteLine(item);
        }

        /// <summary>
        /// Open the data stream from either a specific file or STDIN
        /// </summary>
        /// <returns>A TextReader if successful</returns>
        private TextReader GetInputReader()
        {
            // if there's no file argument, read from STDIN
            if (String.IsNullOrWhiteSpace(Configuration.UriSourceFilePath))
                return Console.In;

            var fs = new FileStream(Configuration.UriSourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var sr = new StreamReader(fs);

            return sr;
        }

        /// <summary>
        /// Get the stream where we're writing the internet out to
        /// </summary>
        /// <returns></returns>
        private XmlWriter GetOutputWriter()
        {
            TextWriter writer = null;

            if (String.IsNullOrWhiteSpace(Configuration.OutputDataFilePath))
                writer = Console.Out;
            else
            {
                string filename = Utility.CreateNewFileOrIndexNameIfExists(Configuration.OutputDataFilePath);
                writer = new StreamWriter(new FileStream(filename, FileMode.Open, FileAccess.Write, FileShare.Read));
            }

            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.NewLineHandling = NewLineHandling.Replace;
            return XmlWriter.Create(writer, settings);
        }

        private void OpenNewOutputWriter()
        {
            outputWriter = GetOutputWriter();
            outputWriter.WriteStartDocument();
            outputWriter.WriteStartElement("resources");
            outputWriter.WriteStartElement("startTime");
            outputWriter.WriteValue(DateTime.UtcNow);
            outputWriter.WriteEndElement();
        }

        private void CloseOutputWriter()
        {
            outputWriter.WriteStartElement("endTime");
            outputWriter.WriteValue(DateTime.UtcNow);
            outputWriter.WriteEndElement();
            outputWriter.WriteEndElement(); // resources
            outputWriter.WriteEndDocument();
            outputWriter.Flush();

            if (!String.IsNullOrWhiteSpace(Configuration.OutputDataFilePath))
            {
                outputWriter.Close();
                outputWriter.Dispose();
            }
        }

        private void CloseRequeueWriter()
        {
            requeueWriter.Close();
            requeueWriter.Dispose();
            requeueWriter = null;
        }

        private void RunPreStartChecks()
        {
            // not sure why it thinks this - probably comparing ints rather than longs
            if (Settings.MaxFileDownloadLengthInBytes * (long)MaxConcurrentFetches > (long)4096 * 1024 * 1024)
#pragma warning disable CS0162 // Unreachable code detected
                log.WarnFormat("MaxConcurrentFetches * MaxFileDownloadLengthInBytes is greater than 4GB. For safety this should not be this big");
#pragma warning restore CS0162 // Unreachable code detected
        }

        private TextWriter GetRequeueWriter()
        {
            if (String.IsNullOrEmpty(Configuration.RequeueUrlsFilePath))
                return null;
            return new StreamWriter(new FileStream(Configuration.RequeueUrlsFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read));
        }

        /// <summary>
        /// Creates the pressure relief valve to throw out tasks
        /// </summary>
        /// <returns></returns>
        private PressureReliefValve<QueueItem> CreatePressureReliefValve()
        {
            var prv = new PressureReliefValve<QueueItem>(PressureReliefThreshold);

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
                MinPressureReliefValveWaitTimeInMilliseconds,
                MaxPressureReliefValveWaitTimeInMilliseconds
                );

        private async Task<IPAddress> GetQueueItemTargetIP(QueueItem item) =>
            !item.TargetIP.Equals(IPAddress.None) ? item.TargetIP : await Utility.GetHostIPAddress(item.TargetUri);

        private void LogStatus(string status) =>
            log.InfoFormat("{0}: Active Fetches {1}, Completed {2}, Waiting for IP {3}, Waiting For Fetch Timeout {4}, Waiting to Write: {5}, Spooling time {6}, Active Chunks {7}",
                status,
                    valve.TasksInValve,
                    completedFetches,
                    valve.TasksWaiting,
                    waitingForFetchTimeout,
                    ResourceFetcher.WaitingToWrite,
                    lastSpoolingTime,
                    MaxConcurrentFetches - fetchLock.CurrentCount);

    }
}

