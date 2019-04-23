using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class ScratchTests
    {
        [TestMethod]
        public async Task RunTests()
        {
            DateTime startTime = DateTime.UtcNow;
            IPAddress[] ips = null;
            for (int i = 0; i < 10000; i++)
                ips = await Dns.GetHostAddressesAsync("www.google.com");
            Assert.IsTrue(false, (DateTime.UtcNow - startTime).TotalMilliseconds.ToString());
        }

        [TestMethod]
        public void RegexTest()
        {
            var regex = new Regex("(?<caffeine>caffeine)|(?<coffee>coffee)");

            Assert.IsTrue(regex.IsMatch("coffee is the elixir of life"));
            var match = regex.Match("coffee is the elixir of life");
            Assert.IsTrue(match.Success);
            foreach (var g in match.Groups) Console.WriteLine(g);
//            Assert.IsTrue(match.Groups.Count == 1, match.Groups.Aggregate("", (x, y) => x + " " + y));
        }

        [TestMethod]
        public void RegexUnnamedCapturesTest()
        {
            var regex = new Regex(@"[a-zA-Z]{2,10}\scoffee\s[a-zA-Z]{2,10}");

            Assert.IsTrue(regex.IsMatch("black coffee is the elixir of life"));
            var match = regex.Match("blue coffee is the elixir of life");
            Assert.IsTrue(match.Success);
            foreach (var g in match.Groups) Console.WriteLine(g);
            foreach (var c in match.Captures) Console.WriteLine(c.ToString());
            //            Assert.IsTrue(match.Groups.Count == 1, match.Groups.Aggregate("", (x, y) => x + " " + y));
        }

        [TestMethod]
        public void RegexTest2()
        {
            const string Example = "The farmer grew seventeen thousand varities of coffee beens for his children. He grew them in an attempt to cure the childrens sleep addiction.";
            var regex = new Regex(@"(\s*[\w,.]+\s*){2,6}coffee(\s*[\w,.]+\s*){2,6}");

            Assert.IsTrue(regex.IsMatch(Example));
            var match = regex.Match(Example);
            Assert.IsTrue(match.Success);
            foreach (var g in match.Groups) Console.WriteLine(g);
            foreach (var c in match.Captures) Console.WriteLine(c.ToString());
            //            Assert.IsTrue(match.Groups.Count == 1, match.Groups.Aggregate("", (x, y) => x + " " + y));
        }
    }
}
