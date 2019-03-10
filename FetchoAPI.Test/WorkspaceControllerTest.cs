using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Formatting;
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

            var guid = new Guid("f5201ff7-ea59-4e00-87b9-af4a0a9c8e2e");
            var httpResponse = await controller.GetResultsByWorkspace(guid, 0, 30);

            Assert.IsNotNull(httpResponse);
            var results = await httpResponse.Content.ReadAsAsync<IEnumerable<WorkspaceResult>>(new[] { new JsonMediaTypeFormatter() });

            await controller.PostResultsByWorkspace(guid, results);

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
