using System;
using System.Diagnostics;
using Fetcho.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace getlinks_core.tests
{
    [TestClass]
    public class IndexableQueueTest
    {
        const int NumberOfItemsToTest = 1000000;

        [TestMethod]
        public void EnqueueTest()
        {
            var queue = new IndexableQueue<int>();

            for (int i = 0; i < NumberOfItemsToTest; i++)
                queue.Enqueue(i);
        }

        [TestMethod]
        public void DequeueTest()
        {
            var queue = new IndexableQueue<int>();

            for (int i = 0; i < NumberOfItemsToTest; i++)
                queue.Enqueue(i);

            for (int i = 0; i < NumberOfItemsToTest; i++)
                Assert.IsTrue(i == queue.Dequeue());

            Assert.IsTrue(queue.Count == 0);

        }

        [TestMethod]
        public void RemoveTest()
        {
            var queue = new IndexableQueue<int>(NumberOfItemsToTest);

            for (int i = 0; i < NumberOfItemsToTest; i++)
                queue.Enqueue(i);

            Stopwatch watch = new Stopwatch();
            watch.Start();
            queue.Remove(NumberOfItemsToTest / 2);
            watch.Stop();

            long time_one = watch.ElapsedTicks;

            watch.Reset();
            watch.Start();
            queue.Dequeue();
            watch.Stop();

            long time_two = watch.ElapsedTicks;

            long diff = Math.Abs(time_one - time_two);

            // there are 10,000 ticks to a millisecond.
            Assert.IsTrue(diff < 1000,
                           string.Format("{0}ticks - {1}ticks = {2}ticks",
                                         time_one, time_two, diff)
                         );

        }
    }
}
