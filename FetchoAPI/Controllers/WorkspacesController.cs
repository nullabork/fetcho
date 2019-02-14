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

        /// <summary>
        /// Get a workspace by it's ID
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        // GET: api/workspaces/{guid}
        [Route("api/workspaces/{guid}")]
        [HttpGet()]
        public async Task<Workspace> Get(Guid guid)
        {
            Workspace workspace = null;
            using (var db = new Database())
                workspace = await db.GetWorkspace(guid);
            return workspace;
        }

        /// <summary>
        /// Create a new workspace
        /// </summary>
        /// <param name="workspace"></param>
        /// <returns></returns>
        // POST: api/workspaces
        [Route("api/workspaces")]
        [HttpPost()]
        public async Task Post([FromBody]Workspace workspace)
        {
            Workspace.Validate(workspace);

            using (var db = new Database())
               await db.SaveWorkspace(workspace);
        }

        /// <summary>
        /// Update a workspace record
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="accesskey"></param>
        /// <param name="workspace"></param>
        /// <returns></returns>
        [Route("api/workspaces/{guid}/accesskey/{accesskey}")]
        [HttpPut()]
        public async Task Put(Guid guid, string accesskey, [FromBody]Workspace workspace)
        {
            Workspace.Validate(workspace);
            TestWorkspaceAndGuidMatch(workspace, guid);

            using (var db = new Database())
            {
                await ThrowIfNoWorkspaceAccess(db, guid, accesskey);
                await db.SaveWorkspace(workspace);
            }
        }

        /// <summary>
        /// Delete a workspace
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="accesskey"></param>
        /// <returns></returns>
        [Route("api/workspaces/{guid}/accesskey/{accesskey}")]
        [HttpDelete()]
        public async Task Delete(Guid guid, string accesskey)
        {
            using (var db = new Database())
            {
                await ThrowIfNoWorkspaceAccess(db, guid, accesskey);
                await db.DeleteWorkspace(guid);
            }
        }

        /// <summary>
        /// Get some results from a workspace
        /// </summary>
        /// <param name="guid">Workspace guid</param>
        /// <param name="minSequence">Minimum sequence value to start from</param>
        /// <param name="count">Max number of results to get</param>
        /// <returns>An enumerable array of results</returns>
        [Route("api/workspaces/{guid}/results")]
        [HttpGet()]
        public IEnumerable<WorkspaceResult> GetResultsByWorkspace(Guid guid, long minSequence = 0, int count = 30)
        {
            count = count.RangeConstraint(1, 50);
            minSequence = minSequence.MinConstraint(0);

            // IEnumerable<WorkspaceResult> results = null;
            // using ( var db = new Database() )
            //   results = db.GetWorkspaceResults(guid, minSequence, count);
            // return results;

            return new WorkspaceResult[] { };
        }

        /// <summary>
        /// Add or update some results in a workspace
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="results"></param>
        [Route("api/workspaces/{guid}/results")]
        [HttpPost()]
        [HttpPut()]
        public void PostResultsByWorkspace(Guid guid, [FromBody]IEnumerable<WorkspaceResult> results)
        {
            // using ( var db = new Database())
            //   db.AddWorkspaceResults(guid, results);
        }

        /// <summary>
        /// Delete some results from a workspace
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="results"></param>
        [Route("api/workspaces/{guid}/results")]
        [HttpDelete()]
        public void DeleteResultsByWorkspace(Guid guid, [FromBody]IEnumerable<WorkspaceResult> results)
        {
            // using ( var db = new Database())
            //   db.DeleteWorkspaceResults(guid, results);
        }

        /// <summary>
        /// Test that the GUID and workspace match - throw if not
        /// </summary>
        /// <param name="workspace"></param>
        /// <param name="guid"></param>
        private void TestWorkspaceAndGuidMatch(Workspace workspace, Guid guid)
        {
            if (workspace.WorkspaceId != guid)
                throw new Exception("Permission denied");
        }

        /// <summary>
        /// Test we have workspace access with this access key
        /// </summary>
        /// <param name="db"></param>
        /// <param name="guid"></param>
        /// <param name="accesskey"></param>
        /// <returns></returns>
        private async Task ThrowIfNoWorkspaceAccess(Database db, Guid guid, string accesskey)
        {
            if (!await db.HasWorkspaceAccess(guid, accesskey))
                throw new Exception("No access to " + guid + " " + accesskey);
        }
    }
}
