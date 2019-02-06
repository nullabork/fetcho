using Fetcho.Common;

namespace Fetcho
{
    public class FetchoConfiguration
    {
        public int MaxActiveFetches { get; set; }

        public string UriSourceFilePath { get; set; }

        public string RequeueUrlsFilePath { get; set; }

        public string OutputDataFilePath { get; set; }

        /// <summary>
        /// If we're only getting raw URLs
        /// </summary>
        public bool InputRawUrls { get; set; }

        public IBlockProvider BlockProvider { get; set; }

        public FetchoConfiguration(string[] args)
        {
            Parse(args);
        }

        private void Parse(string[] args)
        {
            if (args.Length > 0)
                UriSourceFilePath = args[0];

            if (args.Length > 1 && args[1] != "--raw")
                RequeueUrlsFilePath = args[1];

            if (args.Length > 2 && args[2] != "--raw")
                OutputDataFilePath = args[2];

            InputRawUrls |= args.Length > 0 && args[0] == "--raw";
            InputRawUrls |= args.Length > 1 && args[1] == "--raw";
            InputRawUrls |= args.Length > 2 && args[2] == "--raw";
            InputRawUrls |= args.Length > 3 && args[3] == "--raw";
        }
    }
}

