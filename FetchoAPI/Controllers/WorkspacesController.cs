using Fetcho.Common;
using Fetcho.Common.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;

namespace Fetcho.FetchoAPI.Controllers
{
    public class WorkspacesController : ApiController
    {
        // GET: api/workspaces/{accesskey}/workspaces}
        [Route("api/accesskeys/{accesskey}/workspaces")]
        public Task<IEnumerable<Workspace>> Get(string accesskey)
        {
            // IEnumerable<Workspace> workspaces = null;
            // using ( var db = new Database() )
            //   workspaces = db.GetWorkspaces(accesskey);
            // return workspaces;

            return null; // new Workspace[] { Workspace.Create("RAW INTERNET") };
        }

        // GET: api/workspaces/{guid}
        public async Task<Workspace> Get(Guid guid)
        {
            Workspace workspace = null;
            using (var db = new Database())
                workspace = await db.GetWorkspace(guid);
            return workspace;
        }

        // POST: api/workspaces
        public async Task Post([FromBody]Workspace workspace)
        {
            Workspace.Validate(workspace);

            using (var db = new Database())
               await db.SaveWorkspace(workspace);
        }

        // PUT: api/workspaces/{guid}/accesskey/{accesskey}
        public async Task Put(Guid guid, string accesskey, [FromBody]Workspace workspace)
        {
            Workspace.Validate(workspace);
            TestWorkspaceAndGuidMatch(workspace, guid);

            using (var db = new Database())
            {
                await TestHasWorkspaceAccess(db, guid, accesskey);
                await db.SaveWorkspace(workspace);
            }
        }

        // DELETE: api/Workspaces/{guid}/accesskey/{accesskey}
        public async Task Delete(Guid guid, string accesskey)
        {
            using (var db = new Database())
            {
                await TestHasWorkspaceAccess(db, guid, accesskey);
                await db.DeleteWorkspace(guid);
            }
        }

        [Route("api/workspaces/{guid}/results")]
        [HttpGet()]
        public IEnumerable<WorkspaceResult> GetResultsByWorkspace(Guid guid, long minSequence, int count)
        {
            count = count.RangeConstraint(1, 50);
            minSequence = minSequence.MinConstraint(0);

            // IEnumerable<WorkspaceResult> results = null;
            // using ( var db = new Database() )
            //   results = db.GetWorkspaceResults(guid, minSequence, count);
            // return results;

            return new WorkspaceResult[] { };
        }

        [Route("api/workspaces/{guid}/results")]
        [HttpPost()]
        [HttpPut()]
        public void PostResultsByWorkspace(Guid guid, [FromBody]IEnumerable<WorkspaceResult> results)
        {
            // using ( var db = new Database())
            //   db.AddWorkspaceResults(guid, results);
        }

        [Route("api/workspaces/{guid}/results")]
        [HttpDelete()]
        public void DeleteResultsByWorkspace(Guid guid, [FromBody]IEnumerable<WorkspaceResult> results)
        {
            // using ( var db = new Database())
            //   db.DeleteWorkspaceResults(guid, results);
        }

        private void TestWorkspaceAndGuidMatch(Workspace workspace, Guid guid)
        {
            if (workspace.WorkspaceId != guid)
                throw new Exception("Permission denied");
        }

        private async Task TestHasWorkspaceAccess(Database db, Guid guid, string accesskey)
        {
            if (!await db.HasWorkspaceAccess(guid, accesskey))
                throw new Exception("No access");
        }
    }
}
