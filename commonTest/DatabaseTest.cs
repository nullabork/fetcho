
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Fetcho.Common.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class DatabaseTest
    {
        [TestMethod]
        public void TestMethod()
        {
            var uri = new Uri("https://www.site1.com");
            var site = new Site() { HostName = uri.Host };

            Assert.IsTrue(site.Hash.Values.Length > 0, "No hash");
            Assert.IsTrue(site.HostName == "www.site1.com");
            Assert.IsTrue(site.IsBlocked == false);
            Assert.IsTrue(site.LastRobotsFetched == null);

            var stopwatch = new Stopwatch();
            using (var db = new Database("Server=127.0.0.1;Port=5432;User Id=getlinks;Password=getlinks;Database=fetcho;Enlist=false"))
            {
                stopwatch.Start();
                db.SaveSite(site).GetAwaiter().GetResult();
                stopwatch.Stop();
                //Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1, stopwatch.ElapsedMilliseconds.ToString());

                site = db.GetSite(uri).GetAwaiter().GetResult();
                Assert.IsTrue(site != null, "Site was null");
            }

            Random random = new Random();
            stopwatch.Reset();
            stopwatch.Start();

            for (int i = 0; i < 5000; i++)
            {
                using (var db = new Database("Server=127.0.0.1;Port=5432;User Id=getlinks;Password=getlinks;Database=fetcho;Enlist=false"))
                {
                    db.SaveWebResource(new Uri("http://" + random.Next(0, 10000000) ), DateTime.Now).GetAwaiter().GetResult();
                }
            }
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1, stopwatch.ElapsedMilliseconds.ToString());

            stopwatch.Stop();
        }

        [TestMethod]
        public async Task GetSiteTest()
        {
            var uri = new Uri("https://omim.org/contact");

            using (var db = new Database("Server=127.0.0.1;Port=5432;User Id=getlinks;Password=getlinks;Database=fetcho;Enlist=false"))
            {
                var site = await db.GetSite(uri);
                Assert.IsNotNull(site);
                Assert.IsNotNull(site.RobotsFile);
                Assert.IsTrue(site.RobotsFile.IsDisallowed(uri));
            }

            var robots = await HostCacheManager.GetRobotsFile(uri.Host);
            Assert.IsNotNull(robots);
            Assert.IsTrue(robots.IsDisallowed(uri));
        }

        [TestMethod]
        public void VisitedTest()
        {
            Uri pandora = new Uri("http://www.pandora.tv/theme/main/8/74");
            Uri drupal = new Uri("http://drupal.org/");
            Uri archaeo = new Uri("https://www.archaeological.org/about/eupdate");
            Uri nonexist = new Uri("https://blahblabhlahblahb");

            using (var db = new Database("Server=127.0.0.1;Port=5432;User Id=getlinks;Password=getlinks;Database=fetcho;Enlist=false"))
            {
                bool rtn = false;

                // the first run is opening the DB connection lazily
                TestUtility.AssertExecutionTimeIsLessThan(() => rtn = db.NeedsVisiting(nonexist).GetAwaiter().GetResult(), 1000);
                Assert.IsTrue(rtn, nonexist.ToString());
                TestUtility.AssertExecutionTimeIsLessThan(() => rtn = db.NeedsVisiting(pandora).GetAwaiter().GetResult(), 20);
                Assert.IsFalse(rtn, pandora.ToString());
                TestUtility.AssertExecutionTimeIsLessThan(() => rtn = db.NeedsVisiting(drupal).GetAwaiter().GetResult(), 20);
                Assert.IsFalse(rtn, drupal.ToString());
                TestUtility.AssertExecutionTimeIsLessThan(() => rtn = db.NeedsVisiting(archaeo).GetAwaiter().GetResult(), 20);
                Assert.IsFalse(rtn, archaeo.ToString());
            }
        }
    }
}
