
using System;

namespace Fetcho.NextLinks
{
    class Program
    {
        public static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            //AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) => Console.Error.WriteLine(eventArgs.Exception);
            //      Application.ThreadException += (sender, eventArgs) => Console.Error.WriteLine(eventArgs.Exception);
            //AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => Console.Error.WriteLine(eventArgs.ExceptionObject);
            var config = new NextLinksConfiguration(args);
            var nextlinks = new NextLinks(config);

            var t = nextlinks.Process();
            System.Threading.Tasks.Task.WaitAll(t);
        }
    }
}