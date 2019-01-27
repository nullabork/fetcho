namespace Fetcho
{
    class Program
    {
        public static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            var config = new FetchoConfiguration(args);
            var fetcho = new Fetcho(config);
            fetcho.Process().GetAwaiter().GetResult();
        }
    }
}