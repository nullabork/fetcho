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
    public class HostCacheManager
    {
        IndexableQueue<string> recencyQueue = null;
        readonly SortedDictionary<string, HostCacheManagerRecord> hosts = new SortedDictionary<string, HostCacheManagerRecord>();
        readonly SemaphoreSlim hosts_lock = new SemaphoreSlim(1);
        readonly Random random = new Random(DateTime.UtcNow.Millisecond);

        public HostCacheManager()
        {
            BuildRecencyQueue();
            FetchoConfiguration.Current.ConfigurationChange += (sender, e)
                => e.IfPropertyIs(() => FetchoConfiguration.Current.HostCacheManagerMaxInMemoryDomainRecords, BuildRecencyQueue);
        }

        /// <summary>
        /// Fetch a robots file
        /// </summary>
        /// <param name="fromHost"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<RobotsFile> GetRobotsFile(string fromHost)
        {
            try
            {
                var record = await GetRecord(fromHost, true);

                if (record.UpdateRobotsFileRequired) // this crazy looking double check is necessary to avoid race conditions
                {
                    try
                    {
                        while (!await record.UpdateWaitHandle.WaitAsync(60000))
                            Utility.LogInfo("GetRobotsFile() waiting on {0}", fromHost);

                        if (record.UpdateRobotsFileRequired)
                        {
                            record.Robots = await RobotsFile.GetFile(new Uri("http://" + fromHost));
                            record.RobotsChecked = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Utility.LogException(ex);
                    }
                    finally
                    {
                        record.UpdateWaitHandle?.Release();
                    }
                }

                return record.Robots;
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
                return null;
            }

        }

        /// <summary>
        /// Delay until we can fetch
        /// </summary>
        /// <param name="hostName"></param>
        /// <returns>True if we can fetch, false if timeout or cancelled</returns>
        public async Task<bool> WaitToFetch(string hostName, int timeoutMilliseconds)
        {
            DateTime startTime = DateTime.UtcNow;
            bool keepWaiting = true;
            bool success = false;
            HostCacheManagerRecord host_record = await GetRecord(hostName, true);

            while (keepWaiting)
            {
                try
                {
                    while (!await host_record.FetchWaitHandle.WaitAsync(360000))
                        Utility.LogInfo("Been waiting a long time to update a record: {0}", host_record.Host);

                    DateTime n = DateTime.UtcNow;
                    if (host_record.IsFetchable)
                    {
                        host_record.LastCall = n;
                        host_record.TouchCount++;
                        keepWaiting = false;
                        success = true;
                    }
                }
                catch (Exception ex)
                {
                    Utility.LogException(ex);
                }
                finally
                {
                    host_record.FetchWaitHandle.Release();
                }

                if (keepWaiting)
                {
                    if (timeoutMilliseconds != Timeout.Infinite && (DateTime.UtcNow - startTime).TotalMilliseconds > timeoutMilliseconds)
                        keepWaiting = false;
                    else
                    {
                        //log.InfoFormat("Waiting on {0}", hostName);
                        await Task.Delay(random.Next(host_record.MaxFetchSpeedInMilliseconds * 7 / 8, host_record.MaxFetchSpeedInMilliseconds * 2));
                    }
                }
            }

            return success;
        }

        public async Task<HostInfo> GetHostInfo(IPAddress ipAddress)
            => await GetHostInfo(ipAddress.ToString());

        public async Task<HostInfo> GetHostInfo(string host)
        {
            HostCacheManagerRecord record = await GetRecord(host);

            if (record == null) return HostInfo.None;

            return record.GetHostInfo();
        }

        public async Task UpdateHostSettings(HostInfo info)
        {
            HostCacheManagerRecord record = await GetRecord(info.Host);
            if (record == null) return;
            record.SetFromHostInfo(info);
        }

        /// <summary>
        /// Record a network issue has occurred trying to connect to this ip address
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        public async Task RecordNetworkIssue(IPAddress ipAddress)
        {
            HostCacheManagerRecord record = await GetRecord(ipAddress.ToString());

            if (record == null) return;

            try
            {
                while (!await record.UpdateWaitHandle.WaitAsync(60000))
                    Utility.LogInfo("RecordNetworkIssue() waiting on {0}", ipAddress);
                record.NetworkIssues++;
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
            finally
            {
                record.UpdateWaitHandle?.Release();
            }
        }

        public async Task<bool> HasHostExceedNetworkIssuesThreshold(IPAddress ipAddress)
        {
            HostCacheManagerRecord record = await GetRecord(ipAddress.ToString());
            return record != null && record.NetworkIssues > FetchoConfiguration.Current.MaxNetworkIssuesThreshold;
        }

        public async Task<bool> WaitToFetch(IPAddress ipAddress, int timeoutMilliseconds)
            => await WaitToFetch(ipAddress.ToString(), timeoutMilliseconds);

        /// <summary>
        /// Returns how much time we'd have to wait before fetching
        /// </summary>
        /// <param name="fromHost"></param>
        /// <returns></returns>
        int GetTimeRemainingToWait(HostCacheManagerRecord host_record)
            => host_record.MaxFetchSpeedInMilliseconds - (int)((DateTime.UtcNow - host_record.LastCall).TotalMilliseconds);

        /// <summary>
        /// Moves a host to the top of the queue preventing it from being dropped out the bottom from disuse
        /// </summary>
        /// <param name="host"></param>
        void BumpHost(string host)
        {
            recencyQueue.Remove(host);
            recencyQueue.Enqueue(host);
        }

        void BuildRecencyQueue()
            => recencyQueue = new IndexableQueue<string>(FetchoConfiguration.Current.HostCacheManagerMaxInMemoryDomainRecords);

        /// <summary>
        /// Retrives a record from the cache or creates a new one
        /// </summary>
        /// <param name="fromHost"></param>
        /// <returns></returns>
        async Task<HostCacheManagerRecord> GetRecord(string fromHost, bool createIfNotExists = false)
        {
            HostCacheManagerRecord record = null;

            try
            {
                while (!await hosts_lock.WaitAsync(360000))
                    Utility.LogInfo("GetRecord waiting {0}", fromHost);

                bool exists = hosts.ContainsKey(fromHost);

                // make it if its not there 
                if (createIfNotExists)
                {
                    if (!exists)
                        hosts.Add(fromHost,
                                    new HostCacheManagerRecord()
                                    {
                                        Host = fromHost,
                                        LastCall = DateTime.MinValue,
                                        RobotsChecked = false,
                                    }
                                   );
                    else
                        // bump the host
                        BumpHost(fromHost);

                    exists = true;
                }

                if (exists)
                {
                    // if there's too many domains in memory we drop one to avoid filling up memory with cached robots objects
                    if (exists && recencyQueue.Count > FetchoConfiguration.Current.HostCacheManagerMaxInMemoryDomainRecords)
                    {
                        string domainToDrop = recencyQueue.Dequeue();

                        record = hosts[domainToDrop];
                        record.Dispose();

                        hosts.Remove(domainToDrop);
                    }

                    record = hosts[fromHost];
                }
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
            finally
            {
                hosts_lock.Release();
            }

            return record;
        }
    }
}
