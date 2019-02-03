
using log4net;
using System;

namespace Fetcho.NextLinks
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            log4net.Config.XmlConfigurator.Configure();
            var log = LogManager.GetLogger(typeof(Program));
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => log.Error(eventArgs.ExceptionObject);
            var config = new NextLinksConfiguration(args);
            var nextlinks = new NextLinks(config);

            var t = nextlinks.Process();
            System.Threading.Tasks.Task.WaitAll(t);
        }
    }
}