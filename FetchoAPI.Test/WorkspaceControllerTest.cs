using System;
using System.Threading.Tasks;
using Fetcho.Common.Entities;
using Fetcho.FetchoAPI.Controllers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FetchoAPI.Test
{
    [TestClass]
    public class WorkspaceControllerTest
    {
        [TestMethod]
        public async Task PostTest()
        {
            var workspace = Workspace.Create("TEST");
            Assert.IsNotNull(workspace, "Workspace is null");

            var controller = new WorkspacesController();
            //await controller.Post(workspace.GetOwnerAccessKey().AccessKey, workspace);

            var httpResponse = await controller.GetResultsByWorkspaceAccessKey("woot-is-awesome", new Guid("8cd40e60-5749-480a-a0e3-77d66f3bb5d6"), 0, 30);

            Assert.IsNotNull(httpResponse);
            //Assert.IsTrue(results.Any());
            //Assert.IsTrue(results.Count() <= 30);

            //var safespace = await controller.Get(workspace.WorkspaceId);
            //Assert.IsNotNull(safespace, "Got nothing back from Get");
            //Assert.IsTrue(workspace.Name == safespace.Name);

            //await controller.Delete(workspace.WorkspaceId, workspace.GetOwnerAccessKey().AccessKey);
            //workspace = await controller.Get(workspace.WorkspaceId);
            //Assert.IsNull(workspace);
        }
    }
}
