using Fetcho.Common;
using Fetcho.Common.DataFlow;
using log4net;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Fetcho
{
    class Program
    {
        const int DefaultBufferBlockLimit = 10;

        public static void Main(string[] args)
        {
            if ( args.Length < 2 )
            {
                Usage();
                return;
            }

            string path = args[0];
            int.TryParse(args[1], out int startPacketIndex);

            // turn on log4net
            log4net.Config.XmlConfigurator.Configure();

            var log = LogManager.GetLogger(typeof(Program));

            // catch all errors and log them
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => log.Error(eventArgs.ExceptionObject);

            // ignore all certificate validation issues
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            // console encoding will now be unicode
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // configure readlinko
            var readLinkoConfig = new ReadLinkoConfiguration();
            readLinkoConfig.DataSourcePath = path;

            // configure fetcho
            var fetchoConfig = new FetchoConfiguration();
            fetchoConfig.DataSourcePath = path;

            // setup the block provider we want to use
            fetchoConfig.BlockProvider = new DefaultBlockProvider();

            // configure queueo
            var queueoConfig = new QueueoConfiguration();
            queueoConfig.QueueOrderingModel = new NaiveQueueOrderingModel();

            // configure nextlinks
            var nextLinksConfig = new NextLinksConfiguration();

            // buffers to connect the seperate tasks together
            BufferBlock<QueueItem> prioritisationBuffer = CreateBufferBlock(DefaultBufferBlockLimit);
            BufferBlock<QueueItem> nextlinksBuffer = CreateBufferBlock(DefaultBufferBlockLimit);
            // really beef this up since it takes for ever to get here
            BufferBlock<QueueItem> fetchQueueBuffer = CreateBufferBlock(DefaultBufferBlockLimit*1000);
            BufferBlock<QueueItem> requeueBuffer = CreateBufferBlock(DefaultBufferBlockLimit);
            BufferBlock<QueueItem> rejectsBuffer = CreateBufferBlock(DefaultBufferBlockLimit);

            // fetcho!
            var readLinko = new ReadLinko(readLinkoConfig, prioritisationBuffer, startPacketIndex);
            var queueo = new Queueo(queueoConfig, prioritisationBuffer, nextlinksBuffer);
            var nextlinks = new NextLinks(nextLinksConfig, nextlinksBuffer, fetchQueueBuffer, rejectsBuffer);
            var fetcho = new Fetcho(fetchoConfig, fetchQueueBuffer, requeueBuffer);
            var requeueWriter = new BufferBlockObjectFileWriter<QueueItem>(fetchoConfig.DataSourcePath, "requeue", requeueBuffer);
            var rejectsWriter = new BufferBlockObjectFileWriter<QueueItem>(fetchoConfig.DataSourcePath, "rejects", rejectsBuffer);

            // execute
            var tasks = new List<Task>();

            tasks.Add(queueo.Process());
            tasks.Add(nextlinks.Process());
            tasks.Add(fetcho.Process());
            tasks.Add(requeueWriter.Process());
            tasks.Add(rejectsWriter.Process());
            tasks.Add(readLinko.Process());

            Task.WaitAll(tasks.ToArray());
        }

        private static void Usage()
        {
            Console.WriteLine("fetcho path start_index");
        }

        private static BufferBlock<QueueItem> CreateBufferBlock(int boundedCapacity) =>
            new BufferBlock<QueueItem>(new DataflowBlockOptions()
            {
                BoundedCapacity = boundedCapacity
            });
    }
}