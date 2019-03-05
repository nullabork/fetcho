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
        [TestMethod]
        public void DisallowedTest()
        {
            var txt = "user-agent: *\n\ndisallow: /data/*\ndisallow: /daylight/$\ndisallow: /jerk\ndisallow: /h*ray.html$";
            var buffer = System.Text.Encoding.UTF8.GetBytes(txt);

            var robots = new RobotsFile(buffer);

            Assert.IsTrue(robots.IsDisallowed(new Uri("http://rofflo.org/data/hooray.html"), FetchoConfiguration.Current.UserAgent));
            Assert.IsTrue(robots.IsDisallowed(new Uri("http://rofflo.org/hooray.html"), FetchoConfiguration.Current.UserAgent));
            Assert.IsTrue(!robots.IsDisallowed(new Uri("http://rofflo.org/daylight/loafo.html"), FetchoConfiguration.Current.UserAgent));
            Assert.IsTrue(robots.IsDisallowed(new Uri("http://rofflo.org/daylight/"), FetchoConfiguration.Current.UserAgent));
            Assert.IsTrue(robots.IsDisallowed(new Uri("http://rofflo.org/jerk"), FetchoConfiguration.Current.UserAgent));
            Assert.IsTrue(!robots.IsDisallowed(new Uri("http://rofflo.org/index.html"), FetchoConfiguration.Current.UserAgent));
            Assert.IsTrue(!robots.IsDisallowed(new Uri("http://rofflo.org/"), FetchoConfiguration.Current.UserAgent));
        }

        [TestMethod]
        public void GetRobotsFileTest()
        {
            Uri robotsUri = new Uri("http://bs.wikipedia.org/robots.txt");
            string robotsFilePath = Path.GetTempFileName() + ".txt";

            using (var client = new WebClient())
            {
                client.DownloadFile(robotsUri, robotsFilePath);
            }

            //      System.Diagnostics.Process.Start(robotsFilePath);

            var robots = new RobotsFile(File.ReadAllBytes(robotsFilePath));

            Assert.IsTrue(robots.IsDisallowed(new Uri("https://bs.wikipedia.org/w/index.php?title=Ujedinjeno_Kraljevstvo&action=edit&section=4"),
                                              FetchoConfiguration.Current.UserAgent));

        }

        [TestMethod]
        public void GetRobotsFileTest2()
        {
            Uri robotsUri = new Uri("http://blogs.wsj.com/robots.txt");
            string robotsFilePath = Path.GetTempFileName() + ".txt";

            using (var client = new WebClient())
            {
                client.DownloadFile(robotsUri, robotsFilePath);
            }

            System.Diagnostics.Process.Start(robotsFilePath);

            var robots = new RobotsFile(File.ReadAllBytes(robotsFilePath));

            Assert.IsFalse(robots.IsDisallowed(new Uri("https://blogs.wsj.com/privateequity"),
                                              FetchoConfiguration.Current.UserAgent));

        }

        [TestMethod]
        public void GetRobotsFileTest4()
        {
            Uri robotsUri = new Uri("https://wiki.dolphin-emu.org/robots.txt");

            string robotsFilePath = Path.GetTempFileName() + ".txt";

            using (var client = new WebClient())
            {
                var data = client.DownloadData(robotsUri);
                File.WriteAllBytes(robotsFilePath, data);
            }

            System.Diagnostics.Process.Start(robotsFilePath);

            var robots = new RobotsFile(File.ReadAllBytes(robotsFilePath));

            Assert.IsFalse(robots.IsDisallowed(new Uri("https://wiki.dolphin-emu.org/index.php?title=Category:Japan_(Release_region)"),
                                               FetchoConfiguration.Current.UserAgent));

        }

        [TestMethod]
        public void GetRobotsFileTest5()
        {
            Uri robotsUri = new Uri("http://pharma.about.com/robots.txt");

            string robotsFilePath = Path.GetTempFileName() + ".txt";

            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "test agent";
                var data = client.DownloadData(robotsUri);
                File.WriteAllBytes(robotsFilePath, data);
                var robots = new RobotsFile(data);

            }

            System.Diagnostics.Process.Start(robotsFilePath);

        }

        [TestMethod]
        public async Task TestUnusualUris()
        {
            Uri uri = new Uri("http://omim.org/contact");
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
            var robots = await RobotsFile.GetFile(uri);
            return robots.IsDisallowed(uri);
        }

    }
}