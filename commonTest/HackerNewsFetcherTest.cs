using System;
using System.Linq;
using System.Threading.Tasks;
using Fetcho.Common.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class HackerNewsFetcherTest
    {
        [TestMethod]
        public async Task FetchTest()
        {
            int numberOfDaysToDownload = 3;
            DateTime start = DateTime.Now.AddDays(-numberOfDaysToDownload);
            for( int i=0;i< numberOfDaysToDownload; i++)
            {
                var s = await HackerNewsFrontPageFetcher.GetLinks(start);
                Assert.IsTrue(s.Any());
                foreach (var u in s)
                    Console.WriteLine(u);
                start = start.AddDays(1);
            }
        }
            
    }
}
