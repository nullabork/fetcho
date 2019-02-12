using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Fetcho.queueo
{
    /// <summary>
    /// Buffers items into queues by some key and flushes them when the maximum queues are reached or maximum for a queue is reached
    /// </summary>
    /// <typeparam name="TKey">Key for queue</typeparam>
    /// <typeparam name="TItem">Item to put in that queue</typeparam>
    /// <remarks>This is useful for turning a random stream of data into a semi-ordered stream of serial chunks</remarks>
    public class QueueBuffer<TKey, TItem>
    {
        static readonly ILog log = LogManager.GetLogger(typeof(QueueBuffer<TKey, TItem>));

        /// <summary>
        /// Maximum number of queues this can contain
        /// </summary>
        public int MaximumNumberOfQueues { get; set; }

        /// <summary>
        /// Maximum queue length before we flush the queue
        /// </summary>
        public int MaximumQueueLength { get; set; }

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
        private QueueBuffer()
        {
        }

        /// <summary>
        /// Add an item to a queue
        /// </summary>
        /// <param name="queueKey"></param>
        /// <param name="item"></param>
        public void Add(TKey queueKey, TItem item)
        {
            CheckIfMaximumNumberOfQueuesReached();

            if (!queues.ContainsKey(queueKey))
                queues.Add(queueKey, new List<TItem>());
            queues[queueKey].Add(item);

            CheckIfMaxQueueLengthReached(queueKey);
        }

        /// <summary>
        /// Clear and remove a queue
        /// </summary>
        /// <param name="queueKey"></param>
        public void Remove(TKey queueKey)
        {
            queues[queueKey].Clear();
            queues.Remove(queueKey);
        }

        public void Clear()
        {
            var keys = queues.Keys.ToArray();
            foreach (var key in keys)
                queues[key].Clear();
            queues.Clear();
        }

        /// <summary>
        /// Flushes the entire queue buffer
        /// </summary>
        public void FlushAllQueues()
        {
            log.Info("FlushAllQueues()");

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
