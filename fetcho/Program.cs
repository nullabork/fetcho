using Fetcho.Common;
using System;
using System.Net;

namespace Fetcho
{
    class Program
    {
        public static void Main(string[] args)
        {
            // ignore all certificate validation issues
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            // console encoding will now be unicode
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // turn on log4net
            log4net.Config.XmlConfigurator.Configure();
            
            // configure fetcho
            var config = new FetchoConfiguration(args);

            // setup the block provider we want to use
            config.BlockProvider = new DefaultBlockProvider();

            // fetcho!
            var fetcho = new Fetcho(config);

            // execute
            fetcho.Process().GetAwaiter().GetResult();

        }
    }
}