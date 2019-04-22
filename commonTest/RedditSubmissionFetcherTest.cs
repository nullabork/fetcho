using System;
using System.Linq;
using Fetcho.Common.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class RedditSubmissionFetcherTest
    {
        [TestMethod]
        public void FetchTest()
        {
            var s = RedditSubmissionFetcher.GetSubmissions("science").GetAwaiter().GetResult();
            var l = s.Count(x => !String.IsNullOrWhiteSpace(x.LinkFlairText));
            Assert.IsFalse(l > 2700, l.ToString());
        }
    }
}
