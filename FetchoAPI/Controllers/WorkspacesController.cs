using Fetcho.Common;
using Fetcho.Common.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;

namespace Fetcho.FetchoAPI.Controllers
{
    // TODO: Alter this so you access all workspaces by accesskey
    // need GetWorkspaceIdByAccessKey()
    // TODO: Access Key you created

    public class WorkspacesController : ApiController
    {
        [Route("api/v1/accesskeys/{accesskey}")]
        [HttpGet()]
        public async Task<IEnumerable<WorkspaceAccessKey>> Get(string accesskey)
        {
            IEnumerable<WorkspaceAccessKey> keys = null;

            using (var db = new Database())
                keys = await db.GetWorkspaceAccessKeys(accesskey);

            return keys;
        }

        /// <summary>
        /// Get a workspace by it's ID
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        [Route("api/v1/accesskeys/{accesskey}/workspace/{accessKeyId}")]
        [HttpGet()]
        public async Task<Workspace> Get(string accesskey, Guid accessKeyId)
        {
            Guid guid = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);

            Workspace workspace = null;
            using (var db = new Database())
                workspace = await db.GetWorkspace(guid);

            //RemoveSecretInformationIfLowerPermissions(workspace, accessKeyId);
            return workspace;
        }

        /// <summary>
        /// Create a new workspace
        /// </summary>
        /// <param name="workspace"></param>
        /// <returns></returns>
        [Route("api/v1/accesskeys/{accesskey}/workspace")]
        [HttpPost()]
        public async Task Post(string accesskey, [FromBody]Workspace workspace)
        {
            Workspace.Validate(workspace);
            //            ThrowIfAccessKeyNotEqualWorkspace(accesskey, workspace);

            using (var db = new Database())
            {
                await db.SaveWorkspace(workspace);
            }
        }

        /// <summary>
        /// Update a workspace record
        /// </summary>
        /// <param name="accesskey"></param>
        /// <param name="accessKeyId"></param>
        /// <param name="workspace"></param>
        /// <returns></returns>
        [Route("api/v1/accesskeys/{accesskey}/workspace/{accessKeyId}")]
        [HttpPut()]
        public async Task Put(string accesskey, Guid accessKeyId, [FromBody]Workspace workspace)
        {
            Workspace.Validate(workspace);
            Guid guid = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);

            using (var db = new Database())
            {
                TestWorkspaceAndGuidMatch(workspace, guid);
                await db.SaveWorkspace(workspace);
            }
        }

        /// <summary>
        /// Delete a workspace access key
        /// </summary>
        /// <param name="accesskey"></param>
        /// <param name="accessKeyId"></param>
        /// <returns></returns>
        [Route("api/v1/accesskeys/{accesskey}/workspace/{accessKeyId}")]
        [HttpDelete()]
        public async Task Delete(string accesskey, Guid accessKeyId)
        {
            Guid guid = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);

            using (var db = new Database())
            {
                await ThrowIfNoWorkspaceAccess(db, guid, accesskey);
                await db.DeleteWorkspace(guid);
            }
        }

        [Route("api/v1/accesskeys/{accesskey}/workspace/{accessKeyId}/results")]
        [HttpGet()]
        public async Task<IEnumerable<WorkspaceResult>> GetResultsByAccessKey(string accesskey, Guid accessKeyId, long fromSequence = 0, int count = 30)
        {
            try
            {
                Guid guid = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);

                return await GetResultsByWorkspace(guid, fromSequence, count);
            }
            catch( Exception ex)
            {
                Utility.LogException(ex);
            }

            return null;
        }

        /// <summary>
        /// Get random results from a workspace
        /// </summary>
        /// <param name="guid">Workspace guid</param>
        /// <param name="minSequence">Minimum sequence value to start from</param>
        /// <param name="count">Max number of results to get</param>
        /// <returns>An enumerable array of results</returns>
        [Route("api/v1/accesskeys/{accesskey}/workspace/{accessKeyId}/results/random")]
        [HttpGet()]
        public async Task<IEnumerable<WorkspaceResult>> GetRandomResultsByAccessKey(string accesskey, Guid accessKeyId, int count = 1)
        {
            try
            {
                Guid guid = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);

                return await GetRandomResultsByWorkspace(guid, count);
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }

            return null;
        }

        [Route("api/v1/accesskeys/{accesskey}/workspace/{accessKeyId}/results")]
        [HttpPost()]
        [HttpPut()]
        public async Task PostResultsByAccessKey(string accesskey, Guid accessKeyId, [FromBody]IEnumerable<WorkspaceResult> results)
        {
            Guid guid = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);

            await PostResultsByWorkspace(guid, results);
        }

        /// <summary>
        /// Get some results from a workspace
        /// </summary>
        /// <param name="guid">Workspace guid</param>
        /// <param name="fromSequence">Minimum sequence value to start from</param>
        /// <param name="count">Max number of results to get</param>
        /// <returns>An enumerable array of results</returns>
        [Route("api/v1/workspaces/{guid}/results")]
        [HttpGet()]
        public async Task<IEnumerable<WorkspaceResult>> GetResultsByWorkspace(Guid guid, long fromSequence = 0, int count = 30)
        {
            try
            {
                count = count.RangeConstraint(1, 50);
                fromSequence = fromSequence.MinConstraint(0);

                IEnumerable<WorkspaceResult> results = null;
                using (var db = new Database())
                    results = await db.GetWorkspaceResults(guid, fromSequence, count);
                return results;
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }

            return null;
        }

        /// <summary>
        /// Get random results from a workspace
        /// </summary>
        /// <param name="guid">Workspace guid</param>
        /// <param name="minSequence">Minimum sequence value to start from</param>
        /// <param name="count">Max number of results to get</param>
        /// <returns>An enumerable array of results</returns>
        [Route("api/v1/workspaces/{guid}/results/random")]
        [HttpGet()]
        public async Task<IEnumerable<WorkspaceResult>> GetRandomResultsByWorkspace(Guid guid, int count = 1)
        {
            try
            {
                count = count.RangeConstraint(1, 50);

                IEnumerable<WorkspaceResult> results = null;
                using (var db = new Database())
                    results = await db.GetWorkspaceResultsByRandom(guid, count);
                return results;
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }

            return null;
        }

        /// <summary>
        /// Add or update some results in a workspace
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="results"></param>
        [Route("api/v1/workspaces/{guid}/results")]
        [HttpPost()]
        [HttpPut()]
        public async Task PostResultsByWorkspace(Guid guid, [FromBody]IEnumerable<WorkspaceResult> results)
        {
            try
            {
                using (var db = new Database())
                    await db.AddWorkspaceResults(guid, results);
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
        }

        /// <summary>
        /// Delete some results from a workspace
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="results"></param>
        [Route("api/v1/workspaces/{guid}/results")]
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
                throw new FetchoException("Permission denied");
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
                throw new FetchoException("No access to " + guid + " " + accesskey);
        }

        private async Task ThrowIfNotAValidAccessKey(Database db, string accesskey)
        {
            if (!await db.IsValidAccessKey(accesskey))
                throw new FetchoException("invalid key");
        }

        private async Task<Guid> GetWorkspaceIdOrThrowIfNoAccess(string accesskey)
        {
            using (var db = new Database())
                return await db.GetWorkspaceIdByAccessKey(accesskey);
        }
    }
}
