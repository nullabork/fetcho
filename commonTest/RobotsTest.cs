using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class RobotsTest
    {
        const string userAgent = "testbot/1.0";
        const string testdataPath = "testdata/RobotsTest/";

        [TestMethod]
        public void DisallowedTest()
        {
            var txt = "user-agent: *\n\ndisallow: /data/*\ndisallow: /daylight/$\ndisallow: /jerk\ndisallow: /h*ray.html$";
            var buffer = System.Text.Encoding.UTF8.GetBytes(txt);

            var robots = new RobotsFile(buffer);

            Assert.IsTrue(robots.IsDisallowed(new Uri("http://rofflo.org/data/hooray.html"), userAgent));
            Assert.IsTrue(robots.IsDisallowed(new Uri("http://rofflo.org/hooray.html"), userAgent));
            Assert.IsTrue(!robots.IsDisallowed(new Uri("http://rofflo.org/daylight/loafo.html"), userAgent));
            Assert.IsTrue(robots.IsDisallowed(new Uri("http://rofflo.org/daylight/"), userAgent));
            Assert.IsTrue(robots.IsDisallowed(new Uri("http://rofflo.org/jerk"), userAgent));
            Assert.IsTrue(!robots.IsDisallowed(new Uri("http://rofflo.org/index.html"), userAgent));
            Assert.IsTrue(!robots.IsDisallowed(new Uri("http://rofflo.org/"), userAgent));
        }

        [TestMethod]
        public void WikipediaTest()
        {
            var path = Path.Combine(testdataPath, "en.wikipedia.org-robots.txt");
            using (var robots = new RobotsFile(File.Open(path, FileMode.Open)))
            {
                Assert.IsTrue(!robots.IsDisallowed(new Uri("https://en.wikipedia.org/wiki/Main_Page"), userAgent));
                Assert.IsTrue(!robots.IsDisallowed(new Uri("https://en.wikipedia.org/wiki/Event_Horizon_Telescope"), userAgent));
                Assert.IsTrue(!robots.IsDisallowed(new Uri("https://en.wikipedia.org/wiki/Talk:Event_Horizon_Telescope"), userAgent));
                Assert.IsTrue(robots.IsDisallowed(new Uri("https://en.wikipedia.org/w/index.php?title=Talk:Event_Horizon_Telescope&action=edit"), userAgent));
                Assert.IsTrue(robots.IsDisallowed(new Uri("https://en.wikipedia.org/wiki/Special:Random"), userAgent));
                Assert.IsTrue(robots.IsDisallowed(new Uri("https://en.wikipedia.org/w/index.php?title=Ahmet_Davutoglu&action=edit&section=34"), userAgent));
            }
        }

        [TestMethod]
        public void SpeedTest()
        {
            // testing that a million URIs can be tested in < 12 seconds (ignoring setup)
            var path = Path.Combine(testdataPath, "en.wikipedia.org-robots.txt");
            using (var robots = new RobotsFile(File.Open(path, FileMode.Open)))
            {
                DateTime startTime = DateTime.Now;
                for (int i = 0; i < 1000000; i++)
                {
                    Assert.IsTrue(!robots.IsDisallowed(new Uri("https://en.wikipedia.org/wiki/Main_Page"), userAgent));
                    Assert.IsTrue(!robots.IsDisallowed(new Uri("https://en.wikipedia.org/wiki/Event_Horizon_Telescope"), userAgent));
                    Assert.IsTrue(!robots.IsDisallowed(new Uri("https://en.wikipedia.org/wiki/Talk:Event_Horizon_Telescope"), userAgent));
                    Assert.IsTrue(robots.IsDisallowed(new Uri("https://en.wikipedia.org/w/index.php?title=Talk:Event_Horizon_Telescope&action=edit"), userAgent));
                    Assert.IsTrue(robots.IsDisallowed(new Uri("https://en.wikipedia.org/wiki/Special:Random"), userAgent));
                }
                var time = DateTime.Now - startTime;
                Assert.IsTrue(time.TotalSeconds < 14, time.ToString());
            }
        }

        [TestMethod]
        public void GithubTest()
        {
            var path = Path.Combine(testdataPath, "www.github.com-robots.txt");
            using (var robots = new RobotsFile(File.Open(path, FileMode.Open)))
            {
                Assert.IsTrue(robots.IsDisallowed(new Uri("https://github.com/nullabork/fetcho/blob/master/README.md"), userAgent));
                Assert.IsTrue(robots.IsNotDisallowed(new Uri("https://github.com/nullabork/fetcho/blob/master/README.md"), "Googlebot"));
            }
        }

        [TestMethod]
        public async Task TestUnusualUris()
        {
            DatabasePool.Initialise(1);

            var uri = new Uri("https://en.wikipedia.org/w/index.php?title=Talk:Event_Horizon_Telescope&action=edit");
            Assert.IsTrue(await VerifyUrlIsBlocked(uri), uri.ToString());

            uri = new Uri("http://omim.org/contact");
            Assert.IsTrue(await VerifyUrlIsBlocked(uri), uri.ToString());

            uri = new Uri("https://www.wikipedia.org/wiki/Burger");
            Assert.IsTrue(!await VerifyUrlIsBlocked(uri), uri.ToString());

            uri = new Uri("https://www.bbc.com/news/world-asia-40360168");
            Assert.IsTrue(!await VerifyUrlIsBlocked(uri), uri.ToString());

            uri = new Uri("https://wiki.dolphin-emu.org/index.php?title=Category:Japan_(Release_region)");
            Assert.IsTrue(!await VerifyUrlIsBlocked(uri), uri.ToString());

        }

        public async Task<bool> VerifyUrlIsBlocked(Uri uri)
        {
            var robots = await RobotsFetcher.GetFile(uri);
            if (robots == null)
                return false;
            return robots.IsDisallowed(uri);
        }

    }
}