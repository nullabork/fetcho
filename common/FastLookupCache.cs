using System.Collections.Generic;

namespace Fetcho.Common
{
    /// <summary>
    /// Fixed size cache for fast lookups of some item
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FastLookupCache<T>
    {
        /// <summary>
        /// HashTable for fast lookups
        /// </summary>
        private HashSet<T> HashTable { get; set; }

        /// <summary>
        /// Internal FIFO queue for figuring out what to drop from the cache when it's full
        /// </summary>
        private Queue<T> FifoQueue { get; set; }

        /// <summary>
        /// Maximum capacity of the cache
        /// </summary>
        public int MaxCapacity { get; set; }

        /// <summary>
        /// Count of the number of items in the cache
        /// </summary>
        public int Count { get => HashTable.Count; }

        /// <summary>
        /// Create a fastlookup cache
        /// </summary>
        /// <param name="maxCapacity"></param>
        public FastLookupCache(int maxCapacity)
        {
            HashTable = new HashSet<T>(maxCapacity);
            FifoQueue = new Queue<T>(maxCapacity);
            MaxCapacity = maxCapacity;
        }

        /// <summary>
        /// Returns true if the cache contains this item
        /// </summary>
        /// <param name="item"></param>
        /// <returns>True if the cache contains this item</returns>
        public bool Contains(T item) => HashTable.Contains(item);

        /// <summary>
        /// Put an item in the queue (may drop an existing item if it's full
        /// </summary>
        /// <param name="item">Item to add</param>
        /// <returns>Item that was dropped if any</returns>
        public T Enqueue(T item)
        {
            T olditem = default(T);
            if (FifoQueue.Count >= MaxCapacity)
                olditem = Dequeue();
            FifoQueue.Enqueue(item);
            HashTable.Add(item);
            return olditem;
        }

        /// <summary>
        /// Remove the oldest item from the cache
        /// </summary>
        /// <returns></returns>
        public T Dequeue()
        {
            var item = FifoQueue.Dequeue();
            HashTable.Remove(item);
            return item;
        }

        /// <summary>
        /// Clear the cache
        /// </summary>
        public void Clear()
        {
            HashTable.Clear();
            FifoQueue.Clear();
        }
    }
}

