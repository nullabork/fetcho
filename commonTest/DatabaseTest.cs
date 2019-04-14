
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

            var robots = await FetchoConfiguration.Current.HostCache.GetRobotsFile(uri.Host);
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
                IEnumerable<MD5Hash> rtn = null;

                // the first run is opening the DB connection lazily
                TestUtility.AssertExecutionTimeIsLessThan(() => rtn = db.NeedsVisiting(nonexist).GetAwaiter().GetResult(), 1000);
                Assert.IsTrue(rtn.Any(), nonexist.ToString());
                TestUtility.AssertExecutionTimeIsLessThan(() => rtn = db.NeedsVisiting(pandora).GetAwaiter().GetResult(), 20);
                Assert.IsFalse(rtn.Any(), pandora.ToString());
                TestUtility.AssertExecutionTimeIsLessThan(() => rtn = db.NeedsVisiting(drupal).GetAwaiter().GetResult(), 20);
                Assert.IsFalse(rtn.Any(), drupal.ToString());
                TestUtility.AssertExecutionTimeIsLessThan(() => rtn = db.NeedsVisiting(archaeo).GetAwaiter().GetResult(), 20);
                Assert.IsFalse(rtn.Any(), archaeo.ToString());
            }
        }

        [TestMethod]
        public async Task VisitedSpeedTest()
        {
            var l = new List<Uri>();
            l.Add(new Uri("http://some.example.com"));
            l.Add(new Uri("http://www.pandora.tv/theme/main/8/74"));
            l.Add(new Uri("http://drupal.org/"));
            l.Add(new Uri("https://www.archaeological.org/about/eupdate"));
            l.Add(new Uri("https://blahblabhlahblahb"));

            using (var db = new Database("Server=127.0.0.1;Port=5433;User Id=postgres;Password=postgres;Database=fetcho;Enlist=false"))
            {
                var hashes = l.Select(x => MD5Hash.Compute(x)).ToList();

                DateTime start = DateTime.UtcNow;
                for (int i = 0; i < 100000; i++)
                    Assert.IsTrue((await db.NeedsVisiting(hashes)).Any());
                Assert.IsTrue(false, (DateTime.UtcNow - start).ToString()); // set n5 (any) set n5 (in): 23.88 33.32 set n1: 17.13 single: 21.73 22.59
            }
        }
    }
}
