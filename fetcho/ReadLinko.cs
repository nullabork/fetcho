using Fetcho.Common;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Fetcho
{
    public class ReadLinko
    {
        static readonly ILog log = LogManager.GetLogger(typeof(ReadLinko));

        public bool Running { get; set; }

        private FetchoConfiguration Configuration { get; set; }
        private BufferBlock<IEnumerable<QueueItem>> PrioritisationBufferOut = null;

        private int CurrentPacketIndex = 0;
        private ReadoProcessor processor = null;
        private ExtractLinksAndBufferConsumer consumer = null;

        public ReadLinko(FetchoConfiguration config, BufferBlock<IEnumerable<QueueItem>> prioritisationBufferOut, int startPacketIndex)
        {
            Running = true;
            Configuration = config;
            PrioritisationBufferOut = prioritisationBufferOut;
            CurrentPacketIndex = startPacketIndex;
            processor = new ReadoProcessor();
            consumer = new ExtractLinksAndBufferConsumer(PrioritisationBufferOut);
            processor.Processor.Consumer = consumer;
        }

        public async Task Process()
        {
            try
            {

                var r = ReportStatus();

                log.Debug("ReadLinko started");

                while (Running)
                {
                    var filename = GetNextFile();
                    log.Debug(filename);
                    try
                    {
                        await processor.Process(filename).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                }

            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                log.Debug("ReadLinko ended");
            }
        }

        public void Shutdown() => Running = false;

        async Task ReportStatus()
        {
            while (true)
            {
                await Task.Delay(Configuration.HowOftenToReportStatusInMilliseconds);
                LogStatus("STATUS UPDATE");
            }
        }

        void LogStatus(string status) =>
        log.InfoFormat(
            "{0}: resources processed {1}, links extracted {2}, outbox {3}",
            status,
            processor.Processor.ResourcesProcessedCount,
            consumer.LinksExtracted,
            PrioritisationBufferOut.Count
            );

        string GetNextFile()
        {
            var filepath = Path.Combine(Configuration.DataSourcePath, "packet-" + CurrentPacketIndex + ".xml");
            if (File.Exists(filepath))
            {
                CurrentPacketIndex++;
            }
            else
            {
                CurrentPacketIndex = 0;
            }
            return filepath;
        }
    }
}