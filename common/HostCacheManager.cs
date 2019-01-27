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
        static readonly SortedDictionary<string, HostCacheManagerRecord> hosts = new SortedDictionary<string, HostCacheManagerRecord>();
        static readonly SemaphoreSlim hosts_lock = new SemaphoreSlim(1);
        static readonly SemaphoreSlim can_i_fetch_lock = new SemaphoreSlim(1);
        static Random random = new Random(DateTime.Now.Millisecond);

        /// <summary>
        /// Fetch a robots file
        /// </summary>
        /// <param name="fromHost"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<RobotsFile> GetRobotsFile(string fromHost, CancellationToken cancellationToken)
        {
            try
            {
                var record = await GetRecord(fromHost, cancellationToken);

                if (record.CheckRobots) // this crazy looking double check is necessary to avoid race conditions
                {
                    try
                    {
                        while (!await record.UpdateWaitHandle.WaitAsync(10000, cancellationToken))
                            log.InfoFormat("GetRobotsFile() waiting on {0}", fromHost);

                        if (record.CheckRobots)
                        {
                            record.RobotsChecked = true;
                            record.Robots = await RobotsFile.GetFile(new Uri("http://" + fromHost), cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                    finally
                    {
                        record.UpdateWaitHandle.Release();
                    }
                }

                return record.Robots;
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
        public static async Task WaitToFetch(string hostName, CancellationToken cancellationToken)
        {
            bool keepWaiting = true;
            HostCacheManagerRecord host_record = await GetRecord(hostName, cancellationToken);

            while (keepWaiting)
            {
                try
                {
                    while (!await host_record.FetchWaitHandle.WaitAsync(10000, cancellationToken))
                        log.InfoFormat("WaitToFetch waiting {0}", hostName);

                    DateTime n = DateTime.Now;
                    if (host_record.IsFetchable)
                    {
                        host_record.LastCall = n;
                        host_record.TouchCount++;
                        keepWaiting = false;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
                finally
                {
                    host_record.FetchWaitHandle.Release();
                }

                if (keepWaiting)
                {
                    log.InfoFormat("Waiting on {0}", hostName);
                    await Task.Delay(random.Next(1, host_record.MaxFetchSpeedInMilliseconds));
                }
            }
        }

        public static async Task WaitToFetch(IPAddress ipAddress, CancellationToken cancellationToken) => await WaitToFetch(ipAddress.ToString(), cancellationToken);



        /// <summary>
        /// Returns how much time we'd have to wait before fetching
        /// </summary>
        /// <param name="fromHost"></param>
        /// <returns></returns>
        static int GetTimeRemainingToWait(HostCacheManagerRecord host_record) => host_record.MaxFetchSpeedInMilliseconds - (int)((DateTime.Now - host_record.LastCall).TotalMilliseconds);

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
        static async Task<HostCacheManagerRecord> GetRecord(string fromHost, CancellationToken cancellationToken)
        {
            // bump the host
            try
            {
                while (!await hosts_lock.WaitAsync(10000, cancellationToken))
                    log.InfoFormat("GetRecord waiting {0}", fromHost);

                BumpHost(fromHost);

                if (!hosts.ContainsKey(fromHost))
                {
                    hosts.Add(fromHost,
                                new HostCacheManagerRecord()
                                {
                                    Host = fromHost,
                                    LastCall = DateTime.MinValue,
                                    RobotsChecked = false,
                                }
                               );
                }

                // if there's too many domains in memory we drop one to avoid filling up memory with cached robots objects
                if (recencyQueue.Count > Settings.HostCacheManagerMaxInMemoryDomainRecords)
                {
                    string domainToDrop = recencyQueue.Dequeue();

                    var record = hosts[domainToDrop];
                    record.Dispose();

                    hosts.Remove(domainToDrop);
                }

                var domain = hosts[fromHost];

                return domain;
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                hosts_lock.Release();
            }

            return null;
        }

        class HostCacheManagerRecord : IDisposable
        {
            public string Host { get; set; }
            public DateTime LastCall { get; set; }
            public int TouchCount { get; set; }
            public int MaxFetchSpeedInMilliseconds { get; set; }
            public SemaphoreSlim UpdateWaitHandle { get; }
            public SemaphoreSlim FetchWaitHandle { get; }
            public RobotsFile Robots { get; set; }
            public bool CheckRobots { get { return !RobotsChecked; } }
            public bool RobotsChecked { get; set; }

            /// <summary>
            /// Returns true if the host has not been accessed recently
            /// </summary>
            /// <param name="record"></param>
            /// <returns></returns>
            public bool IsFetchable { get { return LastCall.AddMilliseconds(MaxFetchSpeedInMilliseconds) < DateTime.Now; } }

            public HostCacheManagerRecord()
            {
                MaxFetchSpeedInMilliseconds = Settings.MaximumFetchSpeedMilliseconds;
                LastCall = DateTime.MinValue;
                UpdateWaitHandle = new SemaphoreSlim(1);
                FetchWaitHandle = new SemaphoreSlim(1);
                RobotsChecked = false;
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
                        UpdateWaitHandle.Dispose();
                        FetchWaitHandle.Dispose();
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
