using System;
using System.Collections.Generic;
using System.Linq;

namespace Fetcho
{
    /// <summary>
    /// Buffers items into queues by some key and flushes them when the maximum queues are reached or maximum for a queue is reached
    /// </summary>
    /// <typeparam name="TKey">Key for queue</typeparam>
    /// <typeparam name="TItem">Item to put in that queue</typeparam>
    /// <remarks>This is useful for turning a random stream of data into a semi-ordered stream of serial chunks</remarks>
    public class QueueBuffer<TKey, TItem>
    {
        /// <summary>
        /// Maximum number of queues this can contain
        /// </summary>
        public int MaximumNumberOfQueues { get; set; }

        /// <summary>
        /// Maximum queue length before we flush the queue
        /// </summary>
        public int MaximumQueueLength { get; set; }

        /// <summary>
        /// Count of total items in the buffer
        /// </summary>
        public int ItemCount { get; private set; }

        private readonly object _itemLock = new object();

        /// <summary>
        /// Action that is run when the queue is flushed
        /// </summary>
        public Action<TKey, IEnumerable<TItem>> ActionWhenQueueIsFlushed { get; set; }

        /// <summary>
        /// The queues
        /// </summary>
        private Dictionary<TKey, List<TItem>> queues = new Dictionary<TKey, List<TItem>>();

        /// <summary>
        /// Create a queue buffer
        /// </summary>
        /// <param name="maximumNumberOfQueues"></param>
        /// <param name="maximumQueueLength"></param>
        public QueueBuffer(int maximumNumberOfQueues, int maximumQueueLength)
        {
            MaximumQueueLength = maximumQueueLength;
            MaximumNumberOfQueues = maximumNumberOfQueues;
        }

        /// <summary>
        /// Default constructor not available
        /// </summary>
        private QueueBuffer() { }

        /// <summary>
        /// Add an item to a queue
        /// </summary>
        /// <param name="queueKey"></param>
        /// <param name="item"></param>
        public void Add(TKey queueKey, TItem item)
        {
            CheckIfMaximumNumberOfQueuesReached();

            lock (_itemLock)
            {
                if (!queues.ContainsKey(queueKey))
                    queues.Add(queueKey, new List<TItem>());
                queues[queueKey].Add(item);
                ItemCount++;
            }

            CheckIfMaxQueueLengthReached(queueKey);
        }

        /// <summary>
        /// Clear and remove a queue
        /// </summary>
        /// <param name="queueKey"></param>
        public void Remove(TKey queueKey)
        {
            lock (_itemLock)
            {
                int c = queues[queueKey].Count;
                queues[queueKey].Clear();
                queues.Remove(queueKey);
                ItemCount -= c;
            }
        }

        /// <summary>
        /// Clear all the items out of all queues
        /// </summary>
        public void Clear()
        {
            lock (_itemLock)
            {
                var keys = queues.Keys.ToArray();
                foreach (var key in keys)
                    queues[key].Clear();
                queues.Clear();
                ItemCount = 0;
            }
        }

        /// <summary>
        /// Flushes the entire queue buffer
        /// </summary>
        public void FlushAllQueues()
        {
            var keys = queues.Keys.ToArray();

            foreach (var key in keys)
                FlushQueue(key);
        }

        /// <summary>
        /// Flush a specific queue
        /// </summary>
        /// <param name="queueKey"></param>
        public void FlushQueue(TKey queueKey)
        {
            ActionWhenQueueIsFlushed(queueKey, queues[queueKey].ToArray());
            Remove(queueKey);
        }

        /// <summary>
        /// Check if a max queue length is reached and flush if so
        /// </summary>
        /// <param name="queueKey"></param>
        private void CheckIfMaxQueueLengthReached(TKey queueKey)
        {
            if (queues[queueKey].Count >= MaximumQueueLength)
                FlushQueue(queueKey);
        }

        /// <summary>
        /// Check if the max number of queues is reached and flush all of them if so
        /// </summary>
        private void CheckIfMaximumNumberOfQueuesReached()
        {
            if (MaximumNumberOfQueues == queues.Count)
                FlushAllQueues();
        }
    }

}
