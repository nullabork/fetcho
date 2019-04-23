using System;
using Fetcho.Common.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class ServerNodeTests
    {

        [TestMethod]
        public void CreateTest()
        {
            var node = new ServerNode();
            Assert.AreEqual(node.Name, Environment.MachineName);
            Assert.IsTrue(node.UriHashRange.Equals(HashRange.Largest));
            Assert.IsTrue(node.ServerId != Guid.Empty);
            Assert.IsFalse(node.IsApproved);
            Console.WriteLine(node);
        }
    }
}
