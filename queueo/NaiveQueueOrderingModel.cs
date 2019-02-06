using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Fetcho.Common;
using log4net;

namespace Fetcho.queueo
{
    /// <summary>
    /// Assigns a random number to the queue item adjusts slightly for common hosts and IP addresses
    /// </summary>
    public class NaiveQueueOrderingModel : IQueuePriorityCalculationModel
    {
        static readonly ILog log = LogManager.GetLogger(typeof(NaiveQueueOrderingModel));
        static readonly Random rand = new Random(DateTime.Now.Millisecond);
        static readonly Dictionary<string, uint> HostCount = new Dictionary<string, uint>();
        static readonly object hostCountLock = new object();

        /// <summary>
        /// For memory safety this is the maximum that be in HostCount before we clear it out and start again
        /// </summary>
        const int MaxHostCountCache = 10000; 

        const int MinDomainsAreEqualSeq = 10 * 1000 * 1000;
        const int MaxDomainsAreEqualSeq = 100 * 1000 * 1000;

        const int MinCommonIPSeq = 10 * 1000 * 1000;
        const int MaxCommonIPSeq = 100 * 1000 * 1000;

        const int MinRandSeq = 0;
        const int MaxRandSeq = 5 * 1000 * 1000;

        const int DoesntNeedVisiting = 750 * 1000 * 1000;

        const uint MultiHostLinkSpreadFactor = 2 * 1000 * 1000;

        /// <summary>
        /// Start a task to calculate the priority for the queueitem
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task CalculatePriority(QueueItem item)
        {
            try
            {
                string host = item.TargetUri.Host;
                uint priority = (uint)rand.Next(MinRandSeq, MaxRandSeq);

                if (!await NeedsVisiting(item))
                    priority += DoesntNeedVisiting;

                if (HostsAreEqual(item))
                    priority += (uint)rand.Next(MinDomainsAreEqualSeq, MaxDomainsAreEqualSeq);
                else
                {
                    try
                    {
                        if (await HostsShareCommonIPAddresses(item))
                            priority += (uint)rand.Next(MinCommonIPSeq, MaxCommonIPSeq);

                    }
                    catch (SocketException ex)
                    {
                        log.InfoFormat("HostsShareCommonIPAddresses socket error - {0}, {1}: {2}", item.SourceUri.Host, item.TargetUri.Host, ex.Message);
                        priority = QueueItem.BadQueueItemPriortyNumber;
                    }
                }

                uint count = 0;
                lock (hostCountLock)
                {
                    if (HostCount.Count >= MaxHostCountCache)
                        HostCount.Clear();

                    if (!HostCount.ContainsKey(host))
                        HostCount.Add(host, 0);

                    count = HostCount[host]++;
                }

                unchecked
                {
                    if (priority + (count * MultiHostLinkSpreadFactor) < priority)
                        priority = uint.MaxValue;
                    else
                        priority += (count * MultiHostLinkSpreadFactor);
                }

                item.Priority = priority;
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        /// <summary>
        /// Returns true if the item needs visiting according to our recency index
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        async Task<bool> NeedsVisiting(QueueItem item)
        {
            try
            {
                bool rtn = false;

                using (var db = new Database())
                {
                    rtn = await db.NeedsVisiting(item.TargetUri);
                }

                return rtn;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return true;
            }
        }

        /// <summary>
        /// Returns true if the source and target hosts are equal
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool HostsAreEqual(QueueItem item) => item.SourceUri.Host == item.TargetUri.Host;

        /// <summary>
        /// Returns true if the source and target hosts share common IPs
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        async Task<bool> HostsShareCommonIPAddresses(QueueItem item)
        {
            var t1 = Dns.GetHostAddressesAsync(item.SourceUri.Host);
            var t2 = Dns.GetHostAddressesAsync(item.TargetUri.Host);

            IPAddress[] sourceips = await t1;
            IPAddress[] targetips = await t2;

            // OPTIMISE?
            for (int i = 0; i < sourceips.Length; i++)
            {
                for (int j = 0; j < targetips.Length; j++)
                {
                    if (sourceips[i].Equals(targetips[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

    }
}

