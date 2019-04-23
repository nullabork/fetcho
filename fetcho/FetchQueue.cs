using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Fetcho.Common;

namespace Fetcho
{
    public class FetchQueue
    {
        private Dictionary<IPAddress, Queue<QueueItem>> Queue { get; set; }
        private readonly object _fetchQueueLock = new object();

        public FetchQueue()
        {
            Queue = new Dictionary<IPAddress, Queue<QueueItem>>();
        }

        public async Task<bool> Enqueue(IEnumerable<QueueItem> items)
        {
            if (!items.Any()) return false;

            bool created = false;
            var addr = await GetQueueItemTargetIP(items.First());
            lock (_fetchQueueLock)
            {
                if (!Queue.ContainsKey(addr))
                {
                    var q = new Queue<QueueItem>();
                    Queue.Add(addr, q);
                    created = true;
                }
                foreach( var item in items )
                    Queue[addr].Enqueue(item);
            }

            return created;
        }

        public QueueItem Dequeue(IPAddress ipAddress)
        {
            QueueItem item = null;

            lock(_fetchQueueLock)
            {
                if (Queue.ContainsKey(ipAddress))
                {
                    item = Queue[ipAddress].Dequeue();
                    if (Queue[ipAddress].Count == 0)
                        Queue.Remove(ipAddress);
                }
            }

            return item;
        }

        public void RemoveQueue(IPAddress ipAddress)
        {
            lock(_fetchQueueLock)
            {
                if (Queue.ContainsKey(ipAddress))
                    Queue.Remove(ipAddress);
            }
        }

        public int QueueCount(IPAddress ipAddress)
        {
            lock(_fetchQueueLock)
            {
                if (Queue.ContainsKey(ipAddress))
                    return Queue[ipAddress].Count;
                return 0;
            }
        }

        private async Task<IPAddress> GetQueueItemTargetIP(QueueItem item)
            => item.TargetIP != null && !item.TargetIP.Equals(IPAddress.None) ? item.TargetIP : await Utility.GetHostIPAddress(item.TargetUri);

    }
}

