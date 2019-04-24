using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class HostCacheManagerTest : BaseTestClass
    {
        [TestMethod]
        public async Task CongestionTest()
        {
            SetupBasicConfiguration();

            var cache = new HostCacheManager();
            var l = new List<Task>();

            var t = ReportStatus();

            for (int i = 0; i < 10000; i++)
            {
                l.Add(WaitAndCount(i, cache));
            }
            await Task.WhenAll(l);

            Assert.IsFalse(t.IsFaulted);
        }

        static int waiting = 0;

        private async Task ReportStatus()
        {
            while (true)
            {
                await Task.Delay(1000);
                Assert.IsTrue(waiting == 0); // we shouldnt have any waiting apart from random
            }
        }

        private async Task WaitAndCount(int count, HostCacheManager cache)
        {
            for (int c = 0; c < 10; c++)
            {
                Interlocked.Increment(ref waiting);
                await cache.WaitToFetch(count.ToString(), Timeout.Infinite);
                Interlocked.Decrement(ref waiting);
                await Task.Delay(FetchoConfiguration.Current.MaxFetchSpeedInMilliseconds+20);
            }

        }
    }


}
