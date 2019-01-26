using System;
using System.IO;

namespace Fetcho
{
    /// <summary>
    /// 
    /// </summary>
    class Extracto
    {
        /// <summary>
        /// The configuration of Extracto
        /// </summary>
        public ExtractoConfiguration Configuration { get; set; }

        /// <summary>
        /// Create an extracto with the passed in configuration
        /// </summary>
        /// <param name="config">Desired configuration</param>
        public Extracto(ExtractoConfiguration config)
        {
            Configuration = config;
        }

        /// <summary>
        /// Begin the extracto process
        /// </summary>
        public void Process()
        {
            var dataStream = getInputStream();
            // TODO: This needs to be smarter to guess on each file change
            var reader = GuessLinkExtractor(dataStream);

            Uri uri = reader.NextUri();

            while (uri != null)
            {
                Console.WriteLine("{0}\t{1}", reader.CurrentSourceUri, uri);

                uri = reader.NextUri();
            }
        }

        /// <summary>
        /// Guess which extractor to use to get links
        /// </summary>
        /// <param name="dataStream"></param>
        /// <returns></returns>
        ILinkExtractor GuessLinkExtractor(TextReader dataStream)
        {
            return new TextFileLinkExtractor(dataStream);
            // return HtmlFileLinkExtractor();
            // return PdfFileLinkExtractor();
            // return ExcelFileLinkExtractor();
            // return CsvFileLinkExtractor();
        }

        /// <summary>
        /// Open the data stream from either a specific file or STDIN
        /// </summary>
        /// <returns>A TextReader if successful</returns>
        TextReader getInputStream()
        {
            // if there's no file argument, read from STDIN
            if (String.IsNullOrWhiteSpace(Configuration.DataSourceFilePath))
                return Console.In;

            var fs = new FileStream(Configuration.DataSourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var sr = new StreamReader(fs);

            return sr;
        }
    }
}

