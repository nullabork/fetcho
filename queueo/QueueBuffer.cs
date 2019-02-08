using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fetcho.queueo
{
    public class QueueBuffer<TKey, TItem>
    {
        public int MaximumNumberOfQueues { get; set; }
        public int MaximumQueueLength { get; set; }

        public Action<TKey, IEnumerable<TItem>> ActionWhenQueueIsFlushed { get; set; }

        private Dictionary<TKey, List<TItem>> queues = new Dictionary<TKey, List<TItem>>();

        public QueueBuffer(int maximumNumberOfQueues, int maximumQueueLength)
        {
            MaximumQueueLength = maximumQueueLength;
            MaximumNumberOfQueues = maximumNumberOfQueues;
        }

        public void Add(TKey key, TItem item)
        {
            if (!queues.ContainsKey(key))
                queues.Add(key, new List<TItem>());
            queues[key].Add(item);

            CheckIfMaxQueueLengthReached(key);
        }

        public void Remove(TKey key)
        {
            queues[key].Clear();
            queues.Remove(key);
        }

        public void Clear()
        {
            foreach (var key in queues.Keys)
                queues[key].Clear();
            queues.Clear();
        }

        public void FlushAllQueues()
        {
            foreach (var key in queues.Keys)
                FlushQueue(key);
        }

        public void FlushQueue(TKey key)
        {
            ActionWhenQueueIsFlushed(key, queues[key].ToArray());
            Remove(key);
        }

        private void CheckIfMaxQueueLengthReached(TKey key)
        {
            if (queues[key].Count >= MaximumQueueLength)
                FlushQueue(key);
        }

        private void CheckIfMaximumNumberOfQueuesReached()
        {
            if (MaximumNumberOfQueues == queues.Count)
                FlushAllQueues();
        }
    }

}
