using System;
using System.Collections.Generic;

namespace Fetcho.Common
{
    /// <summary>
    /// A queue with O(1) removal from anywhere in the queue by object reference
    /// </summary>
    public class IndexableQueue<T> : IDisposable, IEnumerable<T>
    {
        const int DefaultCapacity = 100; 

        LinkItem<T> firstNode;
        LinkItem<T> lastNode;
        Dictionary<T, LinkItem<T>> list;

        public int Count { get => list.Count;  }

        public IndexableQueue(int capacity) =>
            list = new Dictionary<T, LinkItem<T>>(capacity);

        public IndexableQueue() : this(DefaultCapacity)
        {
        }

        public void Enqueue(T item)
        {
            var node = new LinkItem<T>() { Value = item };

            if (firstNode == null)
            {
                firstNode = node;
                lastNode = node;
            }
            else
            {
                lastNode.Next = node;
                node.Prev = lastNode;
                lastNode = node;
            }

            list.Add(item, node);
        }

        /// <summary>
        /// Put an item at the start of the queue
        /// </summary>
        /// <param name="item"></param>
        public void Push(T item)
        {
            var node = new LinkItem<T>() { Value = item };

            if (firstNode == null)
            {
                firstNode = node;
                lastNode = node;
            }
            else
            {
                node.Next = firstNode;
                firstNode = node;
                node.Next.Prev = node;
            }

            list.Add(item, node);
        }

        public T Dequeue()
        {
            if (firstNode == null)
                throw new FetchoException("No items");
            else
            {
                var v = firstNode.Value;
                Remove(v);

                return v;
            }
        }

        public void Remove(T item)
        {
            if (!list.ContainsKey(item))
                return;

            var node = list[item];
            list.Remove(item);

            if (node.Prev != null)
                node.Prev.Next = node.Next;

            if (node.Next != null)
                node.Next.Prev = node.Prev;

            if (node == firstNode)
                firstNode = node.Next;

            if (node == lastNode)
                lastNode = node.Prev;

            node.Next = null;
            node.Prev = null;
            node.Value = default(T);
        }

        public void Clear()
        {
            while (Count > 0)
                Dequeue();
            list.Clear();
            list = null;
        }

        public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => list.Values.GetEnumerator();

        protected virtual void Dispose(bool disposable) => Clear();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private class LinkItem<Q>
        {
            public LinkItem<Q> Prev;
            public LinkItem<Q> Next;
            public Q Value;

            public LinkItem()
            {
                Prev = null;
                Next = null;
                Value = default(Q);
            }
        }

    }
}
