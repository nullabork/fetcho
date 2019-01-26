using log4net;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;


namespace Fetcho.Common
{
    /// <summary>
    /// Manages a cache of host records and robots files
    /// </summary>
    public static class HostCacheManager
    {
        static readonly ILog log = LogManager.GetLogger(typeof(HostCacheManager));
        static readonly IndexableQueue<string> recencyQueue = new IndexableQueue<string>(Settings.HostCacheManagerMaxInMemoryDomainRecords);
        static readonly SortedDictionary<string, HostCacheManagerRecord> domains = new SortedDictionary<string, HostCacheManagerRecord>();
        static readonly SemaphoreSlim domain_record_lock = new SemaphoreSlim(1);
        static readonly SemaphoreSlim can_i_fetch_lock = new SemaphoreSlim(1);
        static Random random = new Random(DateTime.Now.Millisecond);

        /// <summary>
        /// Fetch a robots file
        /// </summary>
        /// <param name="fromHost"></param>
        /// <returns></returns>
        public static async Task<RobotsFile> GetRobotsFile(string fromHost)
        {
            try
            {
                var domain_record = await GetRecord(fromHost, false);

                if (domain_record.Robots == null) // this crazy looking double check is necessary to avoid race conditions
                {
                    try
                    {
                        await domain_record.Semaphore.WaitAsync();
                        if (domain_record.Robots == null)
                            domain_record.Robots = await RobotsFile.GetFile(new Uri("http://" + fromHost));
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                    finally
                    {
                        domain_record.Semaphore.Release();
                    }
                }

                return domain_record.Robots;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return null;
            }

        }

        /// <summary>
        /// Delay until we can fetch
        /// </summary>
        /// <param name="hostName"></param>
        /// <returns></returns>
        public static async Task WaitToFetch(string hostName)
        {
            bool keepWaiting = true;
            HostCacheManagerRecord domain_record = null;

            while (keepWaiting)
            {
                domain_record = await GetRecord(hostName, false);

                try
                {
                    await domain_record.Semaphore.WaitAsync();

                    DateTime n = DateTime.Now;
                    if (HostIsFetchable(domain_record))
                    {
                        domain_record.LastCall = n;
                        domain_record.TouchCount++;
                        keepWaiting = false;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
                finally
                {
                    domain_record.Semaphore.Release();
                }

                if (keepWaiting)
                {
                    log.InfoFormat("Waiting on {0}", hostName);
                    await Task.Delay(random.Next(1, domain_record.MaxFetchSpeedInMilliseconds));
                }
            }
        }

        public static async Task WaitToFetch(IPAddress ipAddress) => await WaitToFetch(ipAddress.ToString());

        /// <summary>
        /// Returns true if the host has not been accessed recently
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        static bool HostIsFetchable(HostCacheManagerRecord record) => record.LastCall.AddMilliseconds(record.MaxFetchSpeedInMilliseconds) < DateTime.Now;

        /// <summary>
        /// Returns how much time we'd have to wait before fetching
        /// </summary>
        /// <param name="fromHost"></param>
        /// <returns></returns>
        static int GetTimeRemainingToWait(HostCacheManagerRecord domain_record) => domain_record.MaxFetchSpeedInMilliseconds - (int)((DateTime.Now - domain_record.LastCall).TotalMilliseconds);

        /// <summary>
        /// Moves a host to the top of the queue preventing it from being dropped out the bottom from disuse
        /// </summary>
        /// <param name="host"></param>
        static void BumpHost(string host)
        {
            recencyQueue.Remove(host);
            recencyQueue.Enqueue(host);
        }

        /// <summary>
        /// Retrives a record from the cache or creates a new one
        /// </summary>
        /// <param name="fromHost"></param>
        /// <param name="fetch_robots"></param>
        /// <returns></returns>
        static async Task<HostCacheManagerRecord> GetRecord(string fromHost, bool fetch_robots = true)
        {
            // optimise: we cold move this robots stuff to within the addition bit
            RobotsFile robots = null;

            if (fetch_robots)
                robots = await RobotsFile.GetFile(new Uri("http://" + fromHost));

            // bump the host
            try
            {
                await domain_record_lock.WaitAsync();

                BumpHost(fromHost);

                if (!domains.ContainsKey(fromHost))
                {
                    domains.Add(fromHost,
                                new HostCacheManagerRecord()
                                {
                                    Host = fromHost,
                                    LastCall = DateTime.MinValue,
                                    Robots = robots,
                                }
                               );
                }

                // if there's too many domains in memory we drop one to avoid filling up memory with cached robots objects
                if (recencyQueue.Count > Settings.HostCacheManagerMaxInMemoryDomainRecords)
                {
                    string domainToDrop = recencyQueue.Dequeue();

                    var record = domains[domainToDrop];
                    record.Dispose();

                    domains.Remove(domainToDrop);
                }

                var domain = domains[fromHost];

                // if we fetched robots and there's already one we need to dispose the current one
                if (domain.Robots != robots)
                {
                    robots?.Dispose();
                    robots = null;
                }

                return domain;
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                domain_record_lock.Release();
            }

            return null;
        }

        class HostCacheManagerRecord : IDisposable
        {
            public string Host { get; set; }
            public DateTime LastCall { get; set; }
            public int TouchCount { get; set; }
            public int MaxFetchSpeedInMilliseconds { get; set; }
            public SemaphoreSlim Semaphore { get; }
            public RobotsFile Robots { get; set; }

            public HostCacheManagerRecord()
            {
                MaxFetchSpeedInMilliseconds = Settings.MaximumFetchSpeedMilliseconds;
                LastCall = DateTime.MinValue;
                Semaphore = new SemaphoreSlim(1);
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        Robots?.Dispose();
                        Semaphore.Dispose();
                    }

                    Robots = null;
                    disposedValue = true;
                }
            }

            // This code added to correctly implement the disposable pattern.
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
            }
            #endregion


        }

    }
}
