namespace Fetcho
{
    class Program
    {
        public static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            var config = new ExtractoConfiguration(args);
            var extracto = new Extracto(config);
            extracto.Process();
        }
    }
}