using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Fetcho.Common;
using log4net;

namespace Fetcho
{
    public class Fetcho
    {
        const int MaxConcurrentFetches = 5000;
        const int HowOftenToReportStatusInMilliseconds = 30000;
        static readonly ILog log = LogManager.GetLogger(typeof(Fetcho));
        int activeFetches = 0;
        int completedFetches = 0;
        public FetchoConfiguration Configuration { get; set; }
        SemaphoreSlim fetchLock = new SemaphoreSlim(MaxConcurrentFetches);
        TextWriter requeueWriter = null;
        XmlWriter outputWriter = null;
        TextReader inputReader = null;

        public Fetcho(FetchoConfiguration config)
        {
            Configuration = config;
        }

        public async Task Process()
        {
            log.Info("Fetcho.Process() commenced");
            requeueWriter = GetRequeueWriter();
            inputReader = GetInputReader();

            OpenNewOutputWriter();
            await FetchUris();
            CloseOutputWriter();
            log.Info("Fetcho.Process() complete");
        }

        async Task FetchUris()
        {
            var u = ParseQueueItem(inputReader.ReadLine());

            while (u != null)
            {
                while (!await fetchLock.WaitAsync(360000))
                    log.InfoFormat("Been waiting a while to fire up a new fetch. Active: {0}, complete: {1}", activeFetches, completedFetches);

                if (u.HasAnIssue)
                    log.InfoFormat("QueueItem has an issue:{0}", u);
                else
                {
                    var t = FetchQueueItem(u);
                }

                u = ParseQueueItem(inputReader.ReadLine());
            }

            await Task.Delay(HowOftenToReportStatusInMilliseconds);

            while (true)
            {
                await Task.Delay(HowOftenToReportStatusInMilliseconds);
                log.InfoFormat("STATUS: Active Fetches {0}, Completed {1}", activeFetches, completedFetches);
                if (activeFetches == 0)
                    return;
            }
        }

        QueueItem ParseQueueItem(string line)
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
        async Task FetchQueueItem(QueueItem item)
        {
            try
            {
                Interlocked.Increment(ref activeFetches);
                await ResourceFetcher.FetchFactory(item.TargetUri, outputWriter, Configuration.BlockProvider, DateTime.MinValue);
            }
            catch (TimeoutException)
            {
                log.InfoFormat("Waited too long to be able to fetch {0}", item.TargetUri);
                OutputItemForRequeuing(item);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                fetchLock.Release();
                Interlocked.Decrement(ref activeFetches);
                Interlocked.Increment(ref completedFetches);
            }
        }

        void OutputItemForRequeuing(QueueItem item) => requeueWriter?.WriteLine(item);

        /// <summary>
        /// Open the data stream from either a specific file or STDIN
        /// </summary>
        /// <returns>A TextReader if successful</returns>
        TextReader GetInputReader()
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
        XmlWriter GetOutputWriter()
        {
            TextWriter writer = null;

            if (String.IsNullOrWhiteSpace(Configuration.OutputDataFilePath))
                writer = Console.Out;
            else 
            {
                string filename = Utility.CreateNewFileIndexNameIfExists(Configuration.OutputDataFilePath);
                writer = new StreamWriter(new FileStream(filename, FileMode.Open, FileAccess.Write, FileShare.Read));
            }

            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.NewLineHandling = NewLineHandling.Replace;
            return XmlWriter.Create(writer, settings);
        }

        void OpenNewOutputWriter()
        {
            outputWriter = GetOutputWriter();
            outputWriter.WriteStartDocument();
            outputWriter.WriteStartElement("resources");
        }

        void CloseOutputWriter()
        {
            outputWriter.WriteEndElement();
            outputWriter.WriteEndDocument();
            outputWriter.Flush();

            if (!String.IsNullOrWhiteSpace(Configuration.OutputDataFilePath))
            {
                outputWriter.Close();
                outputWriter.Dispose();
            }
        }

        TextWriter GetRequeueWriter()
        {
            if (String.IsNullOrEmpty(Configuration.RequeueUrlsFilePath))
                return null;
            return new StreamWriter(new FileStream(Configuration.RequeueUrlsFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read));
        }
    }
}

