using System.Collections.Generic;

namespace Fetcho.queueo
{
    public class FastLookupCache<T>
    {
        public HashSet<T> HashTable { get; set; }
        public Queue<T> FifoQueue { get; set; }
        public int MaxCapacity { get; set; }

        public FastLookupCache(int maxCapacity)
        {
            HashTable = new HashSet<T>(maxCapacity);
            FifoQueue = new Queue<T>(maxCapacity);
            MaxCapacity = maxCapacity;
        }

        public bool Contains(T item) => HashTable.Contains(item);

        public void Enqueue(T item)
        {
            if (FifoQueue.Count > MaxCapacity)
                Dequeue();
            FifoQueue.Enqueue(item);
            HashTable.Add(item);
        }

        public T Dequeue()
        {
            var item = FifoQueue.Dequeue();
            HashTable.Remove(item);
            return item;
        }
    }
}

