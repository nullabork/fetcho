using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class SemaphoreSlimExTest
    {
        [TestMethod]
        public async Task UpdateMaxCountTest()
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(10);

            var l = new List<Task>();

            // spin up more tasks than the semaphore will let through
            for ( int i=0;i<20;i++)
            {
                var waitTask = semaphore.WaitAsync();
                l.Add(waitTask);
            }

            // change the maximum to the number that can go through
            await semaphore.ReleaseOrReduce(10, 21);

            await Task.WhenAll(l.ToArray());

//            Assert.IsTrue(completedInTime, "Did not complete in time, indicates that the semaphore isn't working correctly");
            Assert.IsTrue(semaphore.CurrentCount == 21, semaphore.CurrentCount.ToString());

        }
    }
}
