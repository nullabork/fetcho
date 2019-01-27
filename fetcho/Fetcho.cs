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
        const int MaxConcurrentFetches = 1000;
        const int HowOftenToReportStatusInMilliseconds = 30000;
        static readonly ILog log = LogManager.GetLogger(typeof(Fetcho));
        int activeFetches = 0;
        public FetchoConfiguration Configuration { get; set; }
        SemaphoreSlim fetchLock = new SemaphoreSlim(MaxConcurrentFetches);

        public Fetcho(FetchoConfiguration config)
        {
            Configuration = config;
        }

        public async Task Process()
        {
            var uris = getInputStream();
            await FetchUris(uris);
        }

        async Task FetchUris(TextReader uris)
        {
            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var u = ParseUri(uris.ReadLine());

            while (u != null)
            {
                await fetchLock.WaitAsync(cancellationToken);
                var t = FetchQueueItem(u, cts.Token);

                u = ParseUri(uris.ReadLine());
            }

            await Task.Delay(HowOftenToReportStatusInMilliseconds);

            while (true)
            {
                await Task.Delay(HowOftenToReportStatusInMilliseconds, cancellationToken);
                log.InfoFormat("STATUS: Active Fetches {0}", activeFetches);
                if (activeFetches == 0)
                    return;
            }
        }

        Uri ParseUri(string line)
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
        async Task FetchQueueItem(Uri uri, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref activeFetches);
            await ResourceFetcher.FetchFactory(uri, Console.Out, DateTime.MinValue, cancellationToken);
            fetchLock.Release();
            Interlocked.Decrement(ref activeFetches);
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

