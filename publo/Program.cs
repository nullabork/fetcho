using System;

namespace Fetcho.Publo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            log4net.Config.XmlConfigurator.Configure();
            var config = new PubloConfiguration(args);
            var publo = new Publo(config);
            publo.Process();
        }
    }
}
