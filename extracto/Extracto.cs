using Fetcho.Common;
using log4net;
using System;
using System.IO;

namespace Fetcho
{
    /// <summary>
    /// 
    /// </summary>
    class Extracto
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Extracto));

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
            var packet = new WebDataPacketReader(dataStream);

            do
            {
                var uriReader = GuessLinkExtractor(packet);
                OutputUris(uriReader);
            }
            while (!packet.EndOfFile);

        }

        /// <summary>
        /// Guess which extractor to use to get links
        /// </summary>
        /// <param name="dataStream"></param>
        /// <returns></returns>
        ILinkExtractor GuessLinkExtractor(WebDataPacketReader packet)
        {
            while (packet.NextResource())
            {
                string request = packet.GetRequestString();

                if (string.IsNullOrWhiteSpace(request)) throw new Exception("No good");

                var uri = WebDataPacketReader.GetUriFromRequestString(request);
                if (uri == null) continue;
                var response = packet.GetResponseStream();
                if (response != null)
                    return new TextFileLinkExtractor(uri, new StreamReader(response));

                // if ( response == null ) continue;
                // var contentType = WebDataPacketReader.GetContentTypeFromRequestString(request);
                // switch(contentType)
                // {
                //    case "text/": return new TextFileLinkExtractor...
            }

            return null;
            // return HtmlFileLinkExtractor();
            // return PdfFileLinkExtractor();
            // return ExcelFileLinkExtractor();
            // return CsvFileLinkExtractor();
        }

        void OutputUris(ILinkExtractor reader)
        {
            if (reader == null) return;

            Uri uri = reader.NextUri();

            while (uri != null)
            {
                Console.WriteLine("{0}\t{1}", reader.CurrentSourceUri, uri);

                uri = reader.NextUri();
            }
        }

        /// <summary>
        /// Open the data stream from either a specific file or STDIN
        /// </summary>
        /// <returns>A TextReader if successful</returns>
        Stream getInputStream()
        {
            // if there's no file argument, read from STDIN
            if (String.IsNullOrWhiteSpace(Configuration.DataSourceFilePath))
                return Console.OpenStandardInput();

            return new FileStream(Configuration.DataSourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
    }
}

