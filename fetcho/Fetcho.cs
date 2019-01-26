using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Fetcho.Common;
using log4net;

namespace Fetcho
{
    public class Fetcho
    {
        const int HowOftenToReportStatusInMilliseconds = 30000;
        static readonly ILog log = LogManager.GetLogger(typeof(Fetcho));
        int activeFetches = 0;
        public FetchoConfiguration Configuration { get; set; }

        public Fetcho(FetchoConfiguration config)
        {
            Configuration = config;
        }

        public void Process()
        {
            var uris = getInputStream();
            fetchUris(uris);
        }

        void fetchUris(TextReader uris)
        {
            var tasks = new List<Task>();
            var u = parseUri(uris.ReadLine());

            tasks.Add(ReportStatus());

            while (u != null)
            {
                var l = u;

                tasks.Add(FetchQueueItem(l));

                if (tasks.Count % 100 == 0) Thread.Sleep(25);

                u = parseUri(uris.ReadLine());
            }

            Task.WaitAll(tasks.ToArray());
        }

        Uri parseUri(string line)
        {
            try
            {
                if (Configuration.InputRawUrls)
                    return new Uri(line);
                else
                {
                    var item = QueueItem.Parse(line);
                    if (item == null || item.HasAnIssue)
                        return null;
                    else
                        return item.TargetUri;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
        async Task FetchQueueItem(Uri uri)
        {
            Interlocked.Increment(ref activeFetches);
            await ResourceFetcher.FetchFactory(uri, Console.Out, DateTime.MinValue);
            Interlocked.Decrement(ref activeFetches);
        }

        async Task ReportStatus()
        {
            await Task.Delay(HowOftenToReportStatusInMilliseconds);
            while ( activeFetches > 0 )
            {
                log.InfoFormat("STATUS: Active Fetches {0}", activeFetches);
                await Task.Delay(HowOftenToReportStatusInMilliseconds);
            }
        }

        /// <summary>
        /// Open the data stream from either a specific file or STDIN
        /// </summary>
        /// <returns>A TextReader if successful</returns>
        TextReader getInputStream()
        {
            // if there's no file argument, read from STDIN
            if (String.IsNullOrWhiteSpace(Configuration.UriSourceFilePath))
                return Console.In;

            var fs = new FileStream(Configuration.UriSourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var sr = new StreamReader(fs);

            return sr;
        }
    }
}

