using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class ScratchTests
    {
        [TestMethod]
        public async Task RunTests()
        {
            DateTime startTime = DateTime.Now;
            IPAddress[] ips = null;
            for ( int i=0;i<10000;i++)
                ips = await Dns.GetHostAddressesAsync("www.google.com");
            Assert.IsTrue(false, (DateTime.Now - startTime).TotalMilliseconds.ToString());
        }
    }
}
