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
            await controller.Post(workspace);

            var safespace = await controller.Get(workspace.WorkspaceId);
            Assert.IsNotNull(safespace, "Got nothing back from Get");
            Assert.IsTrue(workspace.Name == safespace.Name);

            await controller.Delete(workspace.WorkspaceId, workspace.GetOwnerAccessKey().AccessKey);
            workspace = await controller.Get(workspace.WorkspaceId);
            Assert.IsNull(workspace);
        }
    }
}
