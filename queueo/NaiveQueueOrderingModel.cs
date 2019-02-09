using System;
using System.Collections.Generic;
using System.Linq;
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
        static readonly Dictionary<IPAddress, uint> HostCount = new Dictionary<IPAddress, uint>();
        static readonly object hostCountLock = new object();

        /// <summary>
        /// For memory safety this is the maximum that be in HostCount before we clear it out and start again
        /// </summary>
        const int MaxHostCountCache = 20000;

        const int MinCommonIPSeq = 10 * 1000 * 1000;
        const int MaxCommonIPSeq = 200 * 1000 * 1000;

        const int MinRandSeq = 0;
        const int MaxRandSeq = 5 * 1000 * 1000;

        const int DoesntNeedVisiting = 750 * 1000 * 1000;
        const int NoResourceFetcherHandler = 750 * 1000 * 1000;
        const int ProbablyBlocked = 1000 * 1000 * 1000;
        const int UnreadableLanguage = 1000 * 1000 * 1000;

        const int HostItemQuotaReachedPriority = 600 * 1000 * 1000;

        const uint MultiHostLinkSpreadFactor = 20 * 1000 * 1000;

        const int HostItemQuota = 15;

        /// <summary>
        /// Start a task to calculate the priority for the queueitem
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        public async Task CalculatePriority(IEnumerable<QueueItem> items)
        {
            IPAddress ipaddr = IPAddress.None;
            uint priority = 0;
            uint basePriority = 0;
            QueueItem f = items.First();

            try
            {
                if (await HostsShareCommonIPAddresses(f))
                    basePriority = (uint)rand.Next(MinCommonIPSeq, MaxCommonIPSeq);
                else
                    basePriority = (uint)rand.Next(MinRandSeq, MaxRandSeq);
            }
            catch (Exception ex)
            {
                log.Error(ex);
                basePriority = (uint)rand.Next(MinCommonIPSeq, MaxCommonIPSeq);
            }

            foreach (var current in items)
            {
                try
                {
                    priority = 0;

                    if (current.UnsupportedUri)
                        priority += NoResourceFetcherHandler;
                    else if (current.IsBlockedByDomain)
                        priority += UnreadableLanguage;
                    else if (current.IsProbablyBlocked)
                        priority += ProbablyBlocked;
                    else if (current.VisitedRecently)
                        priority += DoesntNeedVisiting;
                    else
                    {
                        ipaddr = await Utility.GetHostIPAddress(current.TargetUri);

                        if (ipaddr == IPAddress.None)
                            priority = QueueItem.BadQueueItemPriorityNumber;
                        else
                            priority = basePriority++;
                    }

                    current.Priority = priority;
                }
                catch (SocketException ex)
                {
                    log.InfoFormat("GetHostIPAddress - {0}, {1}: {2}", current.SourceUri.Host, current.TargetUri.Host, ex.Message);
                    current.Priority = QueueItem.BadQueueItemPriorityNumber;
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
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

