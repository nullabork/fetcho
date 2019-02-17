using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class FastLookupCacheTest
    {
        FastLookupCache<char> cache = null;
        char[] chars = new [] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L' };

        [TestInitialize]
        public void Setup()
        {
            cache = new FastLookupCache<char>(10);
            for (int i = 0; i < 10; i++)
                cache.Enqueue(chars[i]);
        }

        [TestMethod]
        public void ContainsTest()
        {
            for (int i = 0; i < 10; i++)
                Assert.IsTrue(cache.Contains(chars[i]), chars[i].ToString());

            Assert.IsTrue(cache.Count == cache.MaxCapacity);

            char c = cache.Enqueue(chars[10]);
            Assert.IsTrue(c == 'A', c.ToString());
            Assert.IsTrue(cache.Contains(chars[10]));

            for ( int i=1;i<10;i++)
                Assert.IsTrue(cache.Contains(chars[i]));

        }

        [TestMethod]
        public void ContainsSpeedTest()
        {
            // on my PC you can run 400000 contains lookups in 10ms with lots of other things running at the same time
            int l = chars.Length;
            TestUtility.AssertExecutionTimeIsLessThan(() => { for (int i = 0; i < 400000; i++) cache.Contains(chars[i % l]);  }, 10);

            l = 1000;
            var c2 = new FastLookupCache<int>(l);
            for (int i = 0; i < c2.MaxCapacity; i++)
                c2.Enqueue(i);

            // for a much larger cache we should get approx. the same timing
            TestUtility.AssertExecutionTimeIsLessThan(() => { for (int i = 0; i < 400000; i++) c2.Contains(i % l); }, 10);
        }

    }
}
