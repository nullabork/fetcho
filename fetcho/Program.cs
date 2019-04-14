using Fetcho.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Fetcho
{
    class Program
    {
        const int DefaultBufferBlockLimit = 20;

        public static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Usage();
                return;
            }

            string paths = args[0];
            int.TryParse(args[1], out int startPacketIndex);

            // turn on log4net
            log4net.Config.XmlConfigurator.Configure();

            // catch all errors and log them
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => Utility.LogException(eventArgs.ExceptionObject as Exception);

            // ignore all certificate validation issues
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            // console encoding will now be unicode
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // database init
            DatabasePool.Initialise();

            // configure fetcho
            var cfg = SetupConfiguration(paths);

            // buffers to connect the seperate tasks together
            BufferBlock<IEnumerable<QueueItem>> prioritisationBuffer = CreateBufferBlock(DefaultBufferBlockLimit);
            // really beef this buffers max size up since it takes for ever accumulate so we dont want to lose any
            BufferBlock<IEnumerable<QueueItem>> fetchQueueBuffer = CreateBufferBlock(DefaultBufferBlockLimit * 100);
            //BufferBlock<IEnumerable<QueueItem>> requeueBuffer = CreateBufferBlock(DefaultBufferBlockLimit);
            //BufferBlock<IEnumerable<QueueItem>> rejectsBuffer = CreateBufferBlock(DefaultBufferBlockLimit);
            BufferBlock<IWebResourceWriter> dataWriterPool = CreateDataWriterPool();

            // fetcho!
            var readLinko = new ReadLinko(prioritisationBuffer, startPacketIndex);
            var queueo = new Queueo(prioritisationBuffer, fetchQueueBuffer, DataflowBlock.NullTarget<IEnumerable<QueueItem>>());
            var fetcho = new Fetcho(fetchQueueBuffer, DataflowBlock.NullTarget<IEnumerable<QueueItem>>(), dataWriterPool);
            var controlo = new Controlo(prioritisationBuffer, fetchQueueBuffer, dataWriterPool);
            var stato = new Stato("stats.csv", fetcho, queueo, readLinko);
            //var requeueWriter = new BufferBlockObjectFileWriter<IEnumerable<QueueItem>>(cfg.DataSourcePath, "requeue", requeueBuffer);
            //var rejectsWriter = new BufferBlockObjectFileWriter<IEnumerable<QueueItem>>(cfg.DataSourcePath, "rejects", new NullTarget);

            // execute
            var tasks = new List<Task>();

            tasks.Add(stato.Process());
            tasks.Add(fetcho.Process());

            //Task.Delay(1000).GetAwaiter().GetResult();
            //tasks.Add(requeueWriter.Process());
            //tasks.Add(rejectsWriter.Process());
            await Task.Delay(1000);
            tasks.Add(queueo.Process());
            await Task.Delay(1000);
            tasks.Add(readLinko.Process());
            tasks.Add(controlo.Process());

            await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);
        }

        private static void Usage()
        {
            Console.WriteLine("fetcho path start_index");
        }

        private static BufferBlock<IEnumerable<QueueItem>> CreateBufferBlock(int boundedCapacity) =>
            new BufferBlock<IEnumerable<QueueItem>>(new DataflowBlockOptions()
            {
                BoundedCapacity = boundedCapacity
            });

        private static BufferBlock<IWebResourceWriter> CreateDataWriterPool()
        {
            var pool = new BufferBlock<IWebResourceWriter>();

            if (!FetchoConfiguration.Current.DataSourcePaths.Any())
                throw new FetchoException("No output data file paths are set");

            foreach ( var path in FetchoConfiguration.Current.DataSourcePaths)
            {
                var packet = CreateNewDataPacketWriter(path);
                pool.SendAsync(packet).GetAwaiter().GetResult();
            }
            return pool;
        }

        private static IWebResourceWriter CreateNewDataPacketWriter(string path)
        {
            string fileName = Path.Combine(path, "packet.xml");

            return new WebDataPacketWriter(fileName);
        }

        private static void CloseAllWriters(BufferBlock<IWebResourceWriter> dataWriterPool)
        {
            while (dataWriterPool.Count > 0)
            {
                var writer = dataWriterPool.Receive();
                writer.Dispose();
            }
        }

        private static FetchoConfiguration SetupConfiguration(string paths)
        {
            var cfg = new FetchoConfiguration();
            FetchoConfiguration.Current = cfg;
            cfg.SetConfigurationSetting<IEnumerable<string>>(() => cfg.DataSourcePaths, paths.Split(','));
            // setup the block provider we want to use
            cfg.BlockProvider = new DefaultBlockProvider();
            // configure queueo
            cfg.QueueOrderingModel = new NaiveQueueOrderingModel();
            // setup host cache manager
            cfg.HostCache = new HostCacheManager();
            // log configuration changes
            cfg.ConfigurationChange += (sender, e) =>
                                        Utility.LogInfo("Configuration setting {0} changed from {1} to {2}",
                                            e.PropertyName,
                                            e.OldValue,
                                            e.NewValue
                                        );

            return cfg;
        }

    }
}