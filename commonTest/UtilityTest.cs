using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class UtilityTest
    {
        public string SomePropertyName1 { get; set; }

        public string SomePropertyName2;

        public static string SomePropertyName3;

        [TestMethod]
        public void GetPropertyNameTest()
        {
            var u = new UtilityTest();

            Assert.IsTrue(Utility.GetPropertyName(() => u.SomePropertyName1) == "SomePropertyName1");
            Assert.IsTrue(Utility.GetPropertyName(() => u.SomePropertyName2) == "SomePropertyName2");
            Assert.IsTrue(Utility.GetPropertyName(() => UtilityTest.SomePropertyName3) == "SomePropertyName3");
        }

        [TestMethod]
        public async Task GetHostAddresses()
        {
            Uri uri = new Uri("https://www.google.com");
            var addresses = await Dns.GetHostAddressesAsync(uri.Host);

            foreach (var addr in addresses)
                Console.WriteLine(addr);

            Assert.IsTrue(addresses.Length == 2);
            Assert.IsTrue(addresses.Where(x => x.AddressFamily == AddressFamily.InterNetwork).Count() == 1);

            var ip = await Utility.GetHostIPAddress(uri, true);
        }
    }
}
