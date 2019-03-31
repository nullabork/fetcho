using Fetcho.Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Fetcho
{
    class Program
    {
        const int DefaultBufferBlockLimit = 20;

        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Usage();
                return;
            }

            string path = args[0];
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

            Thread.Sleep(10000);

            // configure fetcho
            var cfg = new FetchoConfiguration();
            FetchoConfiguration.Current = cfg;
            cfg.SetConfigurationSetting(() => cfg.DataSourcePath, path);
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

            // buffers to connect the seperate tasks together
            BufferBlock<IEnumerable<QueueItem>> prioritisationBuffer = CreateBufferBlock(DefaultBufferBlockLimit);
            // really beef this up since it takes for ever to get here
            BufferBlock<IEnumerable<QueueItem>> fetchQueueBuffer = CreateBufferBlock(DefaultBufferBlockLimit * 100);
            //BufferBlock<IEnumerable<QueueItem>> requeueBuffer = CreateBufferBlock(DefaultBufferBlockLimit);
            //BufferBlock<IEnumerable<QueueItem>> rejectsBuffer = CreateBufferBlock(DefaultBufferBlockLimit);

            // fetcho!
            var readLinko = new ReadLinko(prioritisationBuffer, startPacketIndex);
            var queueo = new Queueo(prioritisationBuffer, fetchQueueBuffer, DataflowBlock.NullTarget<IEnumerable<QueueItem>>());
            var fetcho = new Fetcho(fetchQueueBuffer, DataflowBlock.NullTarget<IEnumerable<QueueItem>>());
            var controlo = new Controlo(prioritisationBuffer, fetchQueueBuffer);
            //var requeueWriter = new BufferBlockObjectFileWriter<IEnumerable<QueueItem>>(cfg.DataSourcePath, "requeue", requeueBuffer);
            //var rejectsWriter = new BufferBlockObjectFileWriter<IEnumerable<QueueItem>>(cfg.DataSourcePath, "rejects", new NullTarget);

            // execute
            var tasks = new List<Task>();

            tasks.Add(fetcho.Process());

            //Task.Delay(1000).GetAwaiter().GetResult();
            //tasks.Add(requeueWriter.Process());
            //tasks.Add(rejectsWriter.Process());
            Task.Delay(1000).GetAwaiter().GetResult();
            tasks.Add(queueo.Process());
            Task.Delay(1000).GetAwaiter().GetResult();
            tasks.Add(readLinko.Process());
            tasks.Add(controlo.Process());

            Task.WaitAll(tasks.ToArray());
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
    }
}