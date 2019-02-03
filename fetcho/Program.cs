using Fetcho.Common;
using System;

namespace Fetcho
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            log4net.Config.XmlConfigurator.Configure();
            var config = new FetchoConfiguration(args);
            config.BlockProvider = new DefaultBlockProvider();
            var fetcho = new Fetcho(config);
            fetcho.Process().GetAwaiter().GetResult();
        }
    }
}