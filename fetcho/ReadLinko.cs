using Fetcho.Common;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Fetcho
{
    public class ReadLinko
    {
        static readonly ILog log = LogManager.GetLogger(typeof(ReadLinko));

        public bool Running { get; set; }

        private BufferBlock<IEnumerable<QueueItem>> PrioritisationBufferOut = null;

        private ReadoProcessor processor = null;
        private ExtractLinksAndBufferConsumer consumer = null;
        private readonly SemaphoreSlim taskPool = new SemaphoreSlim(FetchoConfiguration.Current.MaxConcurrentLinkReaders);

        public int CurrentPacketIndex { get; set; }
        public int CurrentDataSourceIndex { get; set; }
        public long ResourcesProcessed { get => processor.Processor.ResourcesProcessedCount; }
        public long LinksExtracted { get => consumer.LinksExtracted; }
        public int OutboxCount { get => PrioritisationBufferOut.Count; }


        public ReadLinko(BufferBlock<IEnumerable<QueueItem>> prioritisationBufferOut, int startPacketIndex)
        {
            Running = true;
            PrioritisationBufferOut = prioritisationBufferOut;
            CurrentPacketIndex = startPacketIndex;
            processor = new ReadoProcessor();
            consumer = new ExtractLinksAndBufferConsumer(PrioritisationBufferOut);
            processor.Processor.Consumer = consumer;
            FetchoConfiguration.Current.ConfigurationChange += (sender, e) => UpdateConfigurationSettings(e);
        }

        public async Task Process()
        {
            try
            {
                ThrowIfDataSourcePathsIsEmpty();

                log.Debug("ReadLinko started");

                while (Running)
                {
                    var filename = GetNextFile();
                    try
                    {
                        await taskPool.WaitAsync();
                        var u = ProcessFile(filename).ContinueWith(x => taskPool.Release());
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                }

            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
            finally
            {
                log.Debug("ReadLinko ended");
            }
        }

        public void Shutdown() => Running = false;

        async Task ProcessFile(string filename)
        {
            try
            {
                log.Debug(filename);
                await processor.Process(filename).ConfigureAwait(false);
            }
            catch ( Exception ex)
            {
                Utility.LogException(ex);
            }
        }

        string GetNextFile()
        {
            var dataSourcePath = FetchoConfiguration.Current.DataSourcePaths.ElementAt(CurrentDataSourceIndex);
            var filepath = Path.Combine(dataSourcePath, "packet-" + CurrentPacketIndex + ".xml");

            if (File.Exists(filepath))
            {
                CurrentDataSourceIndex++;
                if (CurrentDataSourceIndex >= FetchoConfiguration.Current.DataSourcePaths.Count())
                {
                    CurrentDataSourceIndex = 0;
                    CurrentPacketIndex++;
                }
            }
            // start all over again if we've run out of files
            else if (FetchoConfiguration.Current.DataSourcePaths.Max(x => Directory.GetFiles(x).Length) <= CurrentPacketIndex)
            {
                CurrentPacketIndex = 0;
            }
            return filepath;
        }

        void ThrowIfDataSourcePathsIsEmpty()
        {
            if (!FetchoConfiguration.Current.DataSourcePaths.Any())
                throw new FetchoException("Fetcho needs at least one data source path for packets");
        }

        private void UpdateConfigurationSettings(ConfigurationChangeEventArgs e)
        {
            e.IfPropertyIs(
                 () => FetchoConfiguration.Current.MaxConcurrentLinkReaders,
                 () => UpdateTaskPoolConfiguration(e)
            );
        }

        private void UpdateTaskPoolConfiguration(ConfigurationChangeEventArgs e)
            => taskPool.ReleaseOrReduce((int)e.OldValue, (int)e.NewValue).GetAwaiter().GetResult();

    }
}