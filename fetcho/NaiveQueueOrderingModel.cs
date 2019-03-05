using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Fetcho.Common;
using log4net;

namespace Fetcho
{
    /// <summary>
    /// Assigns a random number to the queue item adjusts slightly for common hosts and IP addresses
    /// </summary>
    public class NaiveQueueOrderingModel : IQueuePriorityCalculationModel
    {
        static readonly ILog log = LogManager.GetLogger(typeof(NaiveQueueOrderingModel));
        static readonly Random rand = new Random(DateTime.Now.Millisecond);

        const int MinHostsAreEqualPriority = 10 * 1000 * 1000;
        const int MaxHostsAreEqualPriority = 200 * 1000 * 1000;

        const int MinRandSeq = 0;
        const int MaxRandSeq = 5 * 1000 * 1000;

        const int DoesntNeedVisiting = 750 * 1000 * 1000;
        const int NoResourceFetcherHandler = 750 * 1000 * 1000;
        const int ProbablyBlocked = 1000 * 1000 * 1000;
        const int UnreadableLanguage = 1000 * 1000 * 1000;

        /// <summary>
        /// Start a task to calculate the priority for the queueitem
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        public void CalculatePriority(IEnumerable<QueueItem> items)
        {
            IPAddress ipaddr = IPAddress.None;
            uint priority = 0;
            uint basePriority = (uint)rand.Next(MinRandSeq, MaxRandSeq);
            QueueItem f = items.First();

            foreach (var current in items)
            {
                priority = 0;

                if (current.UnsupportedUri)
                    priority += NoResourceFetcherHandler;
                else if (HostsAreEqual(current))
                    priority += (uint)rand.Next(MinHostsAreEqualPriority, MaxHostsAreEqualPriority);
                else if (current.IsBlockedByDomain)
                    priority += UnreadableLanguage;
                else if (current.IsProbablyBlocked)
                    priority += ProbablyBlocked;
                else if (current.VisitedRecently)
                    priority += DoesntNeedVisiting;
                else if (current.TargetIP.Equals(IPAddress.None))
                    priority = QueueItem.BadQueueItemPriorityNumber;
                else
                    priority = basePriority++;

                current.Priority = priority;
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
            try
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
            catch (SocketException ex)
            {
                Utility.LogException(ex);
                return true;
            }
        }




    }
}

