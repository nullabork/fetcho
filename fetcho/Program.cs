using Fetcho.Common;
using Fetcho.Common.Dbup;
using Fetcho.Common.Entities;
using Fetcho.Common.Net;
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
        const int DefaultBufferBlockLimit = 200;

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
            await SetupConfiguration(paths);

            // upgrade the database
            DatabaseUpgrader.Upgrade();

            // buffers to connect the seperate tasks together
            BufferBlock<IEnumerable<QueueItem>> prioritisationBuffer = CreateBufferBlock(DefaultBufferBlockLimit);
            // really beef this buffers max size up since it takes for ever accumulate so we dont want to lose any
            BufferBlock<IEnumerable<QueueItem>> fetchQueueBuffer = CreateBufferBlock(DefaultBufferBlockLimit * 1000);
            //BufferBlock<IEnumerable<QueueItem>> requeueBuffer = CreateBufferBlock(DefaultBufferBlockLimit);
            ITargetBlock<IEnumerable<QueueItem>> outboxWriter = CreateOutboxWriter();
            BufferBlock<IWebResourceWriter> dataWriterPool = CreateDataWriterPool();

            // fetcho!
            var readLinko = new ReadLinko(prioritisationBuffer, startPacketIndex);
            var queueo = new Queueo(prioritisationBuffer, fetchQueueBuffer, outboxWriter); // DataflowBlock.NullTarget<IEnumerable<QueueItem>>()
            var fetcho = new Fetcho(fetchQueueBuffer, DataflowBlock.NullTarget<IEnumerable<QueueItem>>(), dataWriterPool);
            var stato = new Stato("stats.csv", fetcho, queueo, readLinko);
            var controlo = new Controlo(prioritisationBuffer, fetchQueueBuffer, dataWriterPool, () =>
            {
                readLinko.Shutdown();
                queueo.Shutdown();
                fetcho.Shutdown();
                stato.Shutdown();
            });
            //var requeueWriter = new BufferBlockObjectFileWriter<IEnumerable<QueueItem>>(cfg.DataSourcePath, "requeue", requeueBuffer);
            //var rejectsWriter = new BufferBlockObjectFileWriter<IEnumerable<QueueItem>>(cfg.DataSourcePath, "rejects", new NullTarget);

            // execute
            var tasks = new List<Task>();

            tasks.Add(stato.Process());
            tasks.Add(fetcho.Process());
            await Task.Delay(1000);
            tasks.Add(queueo.Process());
            await Task.Delay(1000);
            tasks.Add(readLinko.Process());
            tasks.Add(controlo.Process());

            await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);

            CloseAllWriters(dataWriterPool);
            DatabasePool.DestroyAll();
        }

        private static void Usage()
        {
            Console.WriteLine("fetcho paths start_index");
        }

        private static BufferBlock<IEnumerable<QueueItem>> CreateBufferBlock(int boundedCapacity) =>
            new BufferBlock<IEnumerable<QueueItem>>(new DataflowBlockOptions()
            {
                BoundedCapacity = boundedCapacity
            });

        private static ActionBlock<IEnumerable<QueueItem>> CreateOutboxWriter()
        {
            var block = new ActionBlock<IEnumerable<QueueItem>>(items =>
            {
                string path = Path.Combine(FetchoConfiguration.Current.DataSourcePaths.First(), "outbox.txt");
                path = Utility.CreateNewFileOrIndexNameIfExists(path);

                using (var writer = new StreamWriter(path))
                {
                    foreach (var item in items)
                        if ( item.NotInServerScope )
                            writer.WriteLine(item);
                }
            });

            return block;
        }

        private static BufferBlock<IWebResourceWriter> CreateDataWriterPool()
        {
            var pool = new BufferBlock<IWebResourceWriter>();

            if (!FetchoConfiguration.Current.DataSourcePaths.Any())
                throw new FetchoException("No output data file paths are set");

            foreach (var path in FetchoConfiguration.Current.DataSourcePaths)
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

        private static async Task<FetchoConfiguration> SetupConfiguration(string paths)
        {
            var cfg = new FetchoConfiguration();
            FetchoConfiguration.Current = cfg;
            cfg.SetConfigurationSetting(() => cfg.DataSourcePaths, paths.Split(','));

            // setup the block provider we want to use
            cfg.BlockProvider = new DefaultBlockProvider();

            // configure queueo with the queuing model
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
            // get or create a server node with the master
            await GetOrCreateServerNode(cfg);

            return cfg;
        }

        private static async Task GetOrCreateServerNode(FetchoConfiguration cfg)
        {
            if (cfg.FetchoWorkspaceServerBaseUri.Length == 0) return;

            var fetchoClient = new FetchoAPIV1Client(new Uri(cfg.FetchoWorkspaceServerBaseUri));

            cfg.CurrentServerNode = await fetchoClient.GetServerNodeAsync(Environment.MachineName);
            if (cfg.CurrentServerNode == null)
                cfg.CurrentServerNode = await fetchoClient.CreateServerNodeAsync(new ServerNode());
        }

    }
}