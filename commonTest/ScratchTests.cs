﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
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
            for ( int i=0;i<10000;i++)
                ips = await Dns.GetHostAddressesAsync("www.google.com");
            Assert.IsTrue(false, (DateTime.UtcNow - startTime).TotalMilliseconds.ToString());
        }
    }
}
