
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Fetcho.Common;
using Fetcho.Common.entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace commonTest
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
        public async Task RobotsTest()
        {
            var r = await RobotsFile.GetFile(new Uri("https://www.wikipedia.org/"));
            Assert.IsTrue(r.IsNotDisallowed(new Uri("https://en.wikipedia.org/wiki/Burger")), "https://en.wikipedia.org/wiki/Burger is Disallowed according to robots");

            r = await RobotsFile.DownloadRobots(new Uri("https://www.bbc.com/robots.txt"), null);
            Assert.IsTrue(r.IsNotDisallowed(new Uri("https://www.bbc.com/news/world-asia-40360168")));
        }

        [TestMethod]
        public async Task VisitedTest()
        {
            Uri uri = new Uri("http://www.pandora.tv/theme/main/8/74");

            using (var db = new Database("Server=127.0.0.1;Port=5432;User Id=getlinks;Password=getlinks;Database=fetcho;Enlist=false"))
            {
                Assert.IsFalse(await db.NeedsVisiting(uri), "LIES");
            }
        }
    }
}
