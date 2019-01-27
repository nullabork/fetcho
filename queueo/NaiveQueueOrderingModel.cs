using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Fetcho.Common;
using log4net;

namespace Fetcho.queueo
{
    /// <summary>
    /// Assigns a random number to the queue item adjusts slightly for common hosts and IP addresses
    /// </summary>
    public class NaiveQueueOrderingModel : IQueueCalculationModel
    {
        static readonly ILog log = LogManager.GetLogger(typeof(NaiveQueueOrderingModel));
        static readonly Random rand = new Random(DateTime.Now.Millisecond);
        static readonly object hostCountLock = new object();
        static readonly Dictionary<string, int> HostCount = new Dictionary<string, int>();

        const int MinDomainsAreEqualSeq = 1000000;
        const int MaxDomainsAreEqualSeq = 100000000;

        const int MinCommonIPSeq = 1000000;
        const int MaxCommonIPSeq = 100000000;

        const int MinRandSeq = 0;
        const int MaxRandSeq = 75000000;

        const int DoesntNeedVisiting = 750000000;

        const int MultiHostLinkSpreadFactor = 100000;

        public async Task<object> CalculateQueueSequenceNumber(QueueItem item, CancellationToken cancellationToken)
        {
            try
            {
                string host = item.TargetUri.Host;
                int sequence = rand.Next(MinRandSeq, MaxRandSeq);

                if (!await NeedsVisiting(item, cancellationToken))
                    sequence += DoesntNeedVisiting;

                if (DomainsAreEqual(item))
                    sequence += rand.Next(MinDomainsAreEqualSeq, MaxDomainsAreEqualSeq);
                else
                {
                    try
                    {
                        if (await DomainsShareCommonIPAddresses(item, cancellationToken))
                            sequence += rand.Next(MinCommonIPSeq, MaxCommonIPSeq);

                    }
                    catch (SocketException)
                    {
                        sequence = QueueItem.BadQueueItemSequenceNumber;
                    }
                }

                lock(hostCountLock)
                    if (!HostCount.ContainsKey(host))
                        HostCount.Add(host, 0);

                unchecked
                {
                    sequence += HostCount[host] * MultiHostLinkSpreadFactor;
                }

                HostCount[host]++;

                item.Sequence = sequence;
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return null;

        }

        async Task<bool> NeedsVisiting(QueueItem item, CancellationToken cancellationToken)
        {
            try
            {
                bool rtn = false;

                using (var db = new Database())
                {
                    rtn = await db.NeedsVisiting(item.TargetUri, cancellationToken);
                }

                return rtn;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return true;
            }
        }

        bool DomainsAreEqual(QueueItem item) => item.SourceUri.Host == item.TargetUri.Host;

        async Task<bool> DomainsShareCommonIPAddresses(QueueItem item, CancellationToken cancellationToken)
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

