using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Fetcho.Common.Entities;
using Fetcho.Common.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class FetchoAPIV1ClientTest
    {
        FetchoAPIV1Client client;

        [TestInitialize]
        public void Setup()
        {
            var cfg = new FetchoConfiguration();
            FetchoConfiguration.Current = cfg;
            cfg.SetConfigurationSetting(
                () => cfg.FetchoWorkspaceServerBaseUri,
                ConfigurationManager.AppSettings["FetchoWorkspaceServerBaseUri"]
                );
            client = new FetchoAPIV1Client(new Uri(FetchoConfiguration.Current.FetchoWorkspaceServerBaseUri));
        }

        [TestMethod]
        public async Task PostServerNodeAsyncTest()
        {
            var node = new ServerNode();
            var nodeReturned = await client.CreateServerNodeAsync(node);

            Assert.IsTrue(node.Name == nodeReturned.Name);
            Assert.IsTrue(node.ServerId == nodeReturned.ServerId);
            Assert.IsTrue(node.UriHashRange == nodeReturned.UriHashRange);
            Assert.IsTrue(node.Created == nodeReturned.Created);
            Assert.IsTrue(node.IsApproved == nodeReturned.IsApproved);
        }

        [TestMethod]
        public async Task GetServerNodeAsyncTest()
        {
            string machineName = Environment.MachineName;
            var nodeReturned = await client.GetServerNodeAsync(machineName);
            var node = new ServerNode();

            Assert.IsNotNull(nodeReturned, "nodeReturned is null");
            Assert.IsTrue(node.Name == nodeReturned.Name);
            // Assert.IsTrue(node.ServerId == nodeReturned.ServerId); // generated GUIDs will always be different 
            // Assert.IsTrue(node.Created == nodeReturned.Created); // will always be different
            Assert.IsTrue(node.UriHashRange == nodeReturned.UriHashRange);
            Assert.IsTrue(node.IsApproved == nodeReturned.IsApproved);
        }
    }
}
