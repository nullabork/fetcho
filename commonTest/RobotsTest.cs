using System;
using System.IO;
using System.Net;
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

            Assert.IsTrue(robots.IsDisallowed(new Uri("http://rofflo.org/data/hooray.html"), Settings.UserAgent));
            Assert.IsTrue(robots.IsDisallowed(new Uri("http://rofflo.org/hooray.html"), Settings.UserAgent));
            Assert.IsTrue(!robots.IsDisallowed(new Uri("http://rofflo.org/daylight/loafo.html"), Settings.UserAgent));
            Assert.IsTrue(robots.IsDisallowed(new Uri("http://rofflo.org/daylight/"), Settings.UserAgent));
            Assert.IsTrue(robots.IsDisallowed(new Uri("http://rofflo.org/jerk"), Settings.UserAgent));
            Assert.IsTrue(!robots.IsDisallowed(new Uri("http://rofflo.org/index.html"), Settings.UserAgent));
            Assert.IsTrue(!robots.IsDisallowed(new Uri("http://rofflo.org/"), Settings.UserAgent));
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
                                              "ResearchBot"));

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
                                               "ResearchBot"));

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
                                               "ResearchBot"));

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

    }
}