using System;

namespace Fetcho
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            log4net.Config.XmlConfigurator.Configure();
            var config = new ExtractoConfiguration(args);
            var extracto = new Extracto(config);
            extracto.Process();
        }
    }
}