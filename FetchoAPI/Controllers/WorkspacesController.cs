using Fetcho.Common;
using Fetcho.Common.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;

namespace Fetcho.FetchoAPI.Controllers
{
    public class WorkspacesController : ApiController
    {

        #region AccessKeys

        [Route("api/v1/accesskeys/")]
        [HttpPost()]
        public async Task<HttpResponseMessage> PostAccessKey([FromBody]AccessKey accessKey)
        {
            try
            {
                using (var db = new Database())
                {
                    await ThrowIfNotAValidAccessKey(db, accessKey.Key);
                    await db.SaveAccessKey(accessKey);
                }

                return CreateNoContentResponse();
            }
            catch( Exception ex )
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskeys/")]
        [HttpDelete()]
        public async Task<HttpResponseMessage> DeleteAccessKey([FromBody]AccessKey accessKey)
        {
            try
            {
                if (accessKey == null)
                    throw new InvalidRequestFetchoException("No object passed");

                using (var db = new Database())
                {
                    await ThrowIfNotAValidAccessKey(db, accessKey.Key);
                    await db.DeleteAccessKey(accessKey.Key);
                }

                return CreateNoContentResponse();
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskeys/{accesskey}")]
        [HttpGet()]
        public async Task<HttpResponseMessage> Get(string accesskey)
        {
            try
            {
                IEnumerable<WorkspaceAccessKey> keys = null;

                using (var db = new Database())
                {
                    await ThrowIfNotAValidAccessKey(db, accesskey);
                    keys = await db.GetWorkspaceAccessKeys(accesskey);
                }

                return CreateOKResponse(keys);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        /// <summary>
        /// Get a workspace by it's ID
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        [Route("api/v1/accesskeys/{accesskey}/workspace/{accessKeyId}")]
        [HttpGet()]
        public async Task<HttpResponseMessage> Get(string accesskey, Guid accessKeyId)
        {
            try
            {
                Guid guid = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);

                Workspace workspace = null;
                using (var db = new Database())
                    workspace = await db.GetWorkspace(guid);

                //RemoveSecretInformationIfLowerPermissions(workspace, accessKeyId);
                return CreateOKResponse(workspace);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        /// <summary>
        /// Create a new workspace
        /// </summary>
        /// <param name="workspace"></param>
        /// <returns></returns>
        [Route("api/v1/accesskeys/{accesskey}/workspace")]
        [HttpPost()]
        public async Task<HttpResponseMessage> Post(string accesskey, [FromBody]Workspace workspace)
        {
            try
            {
                Workspace.Validate(workspace);
                //            ThrowIfAccessKeyNotEqualWorkspace(accesskey, workspace);

                using (var db = new Database())
                {
                    await db.SaveWorkspace(workspace);
                }

                return CreateCreatedResponse();
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
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
        public async Task<HttpResponseMessage> Put(string accesskey, Guid accessKeyId, [FromBody]Workspace workspace)
        {
            try
            {
                Workspace.Validate(workspace);
                Guid guid = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);

                using (var db = new Database())
                {
                    TestWorkspaceAndGuidMatch(workspace, guid);
                    await db.SaveWorkspace(workspace);
                }

                return CreateNoContentResponse();
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
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
        public async Task<HttpResponseMessage> Delete(string accesskey, Guid accessKeyId)
        {
            try
            {
                Guid guid = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);

                using (var db = new Database())
                {
                    await ThrowIfNoWorkspaceAccess(db, guid, accesskey);
                    await db.DeleteWorkspace(guid);
                }

                return CreateNoContentResponse();
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskeys/{accesskey}/workspace/{accessKeyId}/results")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetResultsByWorkspaceAccessKey(string accesskey, Guid accessKeyId, long fromSequence = 0, int count = 30)
        {
            try
            {
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);
                return await GetResultsByWorkspace(workspaceId, fromSequence, count);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskeys/{accesskey}/workspace/{accessKeyId}/supportedFilters")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetWorkspaceSupportedFiltersByAccessKey(string accesskey, Guid accessKeyId)
        {
            try
            {
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);
                return GetWorkspaceSupportedFilters(workspaceId);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
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
        public async Task<HttpResponseMessage> GetRandomResultsByAccessKey(string accesskey, Guid accessKeyId, int count = 1)
        {
            try
            {
                Guid guid = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);

                return await GetRandomResultsByWorkspace(guid, count);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskeys/{accesskey}/workspace/{accessKeyId}/results")]
        [HttpPost()]
        [HttpPut()]
        public async Task<HttpResponseMessage> PostResultsByAccessKey(string accesskey, Guid accessKeyId, [FromBody]IEnumerable<WorkspaceResult> results)
        {
            try
            {
                Guid guid = await GetWorkspaceIdOrThrowIfNoAccess(accesskey);

                return await PostResultsByWorkspace(guid, results);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }
        #endregion

        #region Workspaces
        /// <summary>
        /// Get some results from a workspace
        /// </summary>
        /// <param name="guid">Workspace guid</param>
        /// <param name="fromSequence">Minimum sequence value to start from</param>
        /// <param name="count">Max number of results to get</param>
        /// <returns>An enumerable array of results</returns>
        [Route("api/v1/workspaces/{guid}/results")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetResultsByWorkspace(Guid guid, long fromSequence = 0, int count = 30)
        {
            try
            {
                count = count.RangeConstraint(1, 50);
                fromSequence = fromSequence.MinConstraint(0);

                IEnumerable<WorkspaceResult> results = null;
                using (var db = new Database())
                    results = await db.GetWorkspaceResults(guid, fromSequence, count);
                return CreateOKResponse(results);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
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
        public async Task<HttpResponseMessage> GetRandomResultsByWorkspace(Guid guid, int count = 1)
        {
            try
            {
                count = count.RangeConstraint(1, 50);

                IEnumerable<WorkspaceResult> results = null;
                using (var db = new Database())
                    results = await db.GetWorkspaceResultsByRandom(guid, count);
                return CreateOKResponse(results);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        /// <summary>
        /// Add or update some results in a workspace
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="results"></param>
        [Route("api/v1/workspaces/{guid}/results")]
        [HttpPost()]
        [HttpPut()]
        public async Task<HttpResponseMessage> PostResultsByWorkspace(Guid guid, [FromBody]IEnumerable<WorkspaceResult> results)
        {
            try
            {
                using (var db = new Database())
                    await db.AddWorkspaceResults(guid, results);

                return CreateCreatedResponse();
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        /// <summary>
        /// Delete some results from a workspace
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="results"></param>
        [Route("api/v1/workspaces/{guid}/results")]
        [HttpDelete()]
        public HttpResponseMessage DeleteResultsByWorkspace(Guid guid, [FromBody]IEnumerable<WorkspaceResult> results)
        {
            try
            {
                // using ( var db = new Database())
                //   db.DeleteWorkspaceResults(guid, results);
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/workspaces/{guid}/supportedFilters")]
        [HttpGet()]
        public HttpResponseMessage GetWorkspaceSupportedFilters(Guid guid)
        {
            try
            {
                var filterTypes = Filter.GetAllFilterTypes();

                var l = new List<string>();
                foreach( var filterType in filterTypes )
                {
                    var attr = filterType.GetCustomAttributes(typeof(FilterAttribute), false).FirstOrDefault() as FilterAttribute;
                    if (attr != null && !attr.Hidden)
                        l.Add(attr.ShortHelp);
                }

                return CreateOKResponse(l);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/workspaces/wellknown")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetWellknownWorkspaces()
        {
            try
            {
                using (var db = new Database())
                {
                    var results = await db.GetWorkspaces();

                    return CreateOKResponse(results.Where(x => x.IsWellKnown));
                }
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        #endregion

        #region helper methods

        /// <summary>
        /// Test that the GUID and workspace match - throw if not
        /// </summary>
        /// <param name="workspace"></param>
        /// <param name="guid"></param>
        private void TestWorkspaceAndGuidMatch(Workspace workspace, Guid guid)
        {
            if (workspace.WorkspaceId != guid)
                throw new PermissionDeniedFetchoException("Permission denied");
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
                throw new PermissionDeniedFetchoException("No access to " + guid + " " + accesskey);
        }

        private async Task ThrowIfNotAValidAccessKey(Database db, string accesskey)
        {
            if (!await db.IsValidAccessKey(accesskey))
                throw new PermissionDeniedFetchoException("invalid key");
        }

        private async Task<Guid> GetWorkspaceIdOrThrowIfNoAccess(string accesskey)
        {
            using (var db = new Database())
                return await db.GetWorkspaceIdByAccessKey(accesskey);
        }

        private HttpResponseMessage CreatePermissionDeniedResponse(PermissionDeniedFetchoException ex)
            => Request.CreateErrorResponse(HttpStatusCode.Forbidden, ex.Message, ex);

        private HttpResponseMessage CreateCreatedResponse()
            => Request.CreateResponse(HttpStatusCode.Created);

        private HttpResponseMessage CreateNoContentResponse()
            => Request.CreateResponse(HttpStatusCode.NoContent);

        private HttpResponseMessage CreateNotImplementedResponse(NotImplementedException ex)
            => Request.CreateResponse(HttpStatusCode.NotImplemented);

        private HttpResponseMessage CreateInvalidRequestResponse(InvalidRequestFetchoException ex)
            => Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex.Message, ex);


        private HttpResponseMessage CreateOKResponse<T>(T payload)
        {
            var resp = Request.CreateResponse(HttpStatusCode.OK, payload, "application/json");
            return resp;
        }

        private HttpResponseMessage CreateExceptionResponse(Exception ex)
        {
            if (ex is PermissionDeniedFetchoException permissionDeniedFetchoException)
                return CreatePermissionDeniedResponse(permissionDeniedFetchoException);
            if (ex is NotImplementedException notImplementedException)
                return CreateNotImplementedResponse(notImplementedException);
            if (ex is InvalidRequestFetchoException invalidRequestFetchoException)
                return CreateInvalidRequestResponse(invalidRequestFetchoException);
            return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message, ex);
        }

        #endregion

    }
}
