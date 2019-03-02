using Fetcho.Common;
using log4net;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Fetcho
{
    public class ReadLinko
    {
        static readonly ILog log = LogManager.GetLogger(typeof(ReadLinko));

        public bool Running { get; set; }

        private ReadLinkoConfiguration Configuration { get; set; }
        private BufferBlock<QueueItem> PrioritisationBufferOut = null;

        private int CurrentPacketIndex = 0;

        public ReadLinko(ReadLinkoConfiguration config, BufferBlock<QueueItem> prioritisationBufferOut, int startPacketIndex)
        {
            Running = true;
            Configuration = config;
            PrioritisationBufferOut = prioritisationBufferOut;
            CurrentPacketIndex = startPacketIndex;
        }

        public async Task Process()
        {
            try
            {

                log.Debug("ReadLinko started");
                var processor = new ReadoProcessor();
                processor.Processor.Consumer = new ExtractLinksAndBufferConsumer(PrioritisationBufferOut);

                while (Running)
                {
                    var filename = GetNextFile();
                    log.Debug(filename);
                    try
                    {
                        await processor.Process(filename).ConfigureAwait(false);
                    }
                    catch(Exception ex)
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

        string GetNextFile()
        {
            var filepath = Path.Combine(Configuration.DataSourcePath, "packet-" + (CurrentPacketIndex++) + ".xml");
            return filepath;
        }
    }
}