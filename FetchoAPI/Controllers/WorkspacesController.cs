using Fetcho.Common;
using Fetcho.Common.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace Fetcho.FetchoAPI.Controllers
{
    public class WorkspacesController : ApiController
    {
        public WorkspacesController()
        {
            log4net.Config.XmlConfigurator.Configure();
        }

        #region AccessKeys

        [Route("api/v1/accounts/")]
        [HttpPost()]
        public async Task<HttpResponseMessage> PostAccount([FromBody]Account account)
        {
            try
            {
                FixAccountObject(account);
                Account.Validate(account);

                using (var db = new Database())
                {
                    await ThrowIfAccountExists(db, account.Name);
                    await db.SaveAccount(account);
                }

                return CreateCreatedResponse(account);
            }
            catch( Exception ex )
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accounts/")]
        [HttpDelete()]
        public async Task<HttpResponseMessage> DeleteAccount([FromBody]Account account)
        {
            try
            {
                if (account == null)
                    throw new InvalidRequestFetchoException("No object passed");

                using (var db = new Database())
                {
                    Account.Validate(account);
                    await ThrowIfNotAValidAccount(db, account.Name);
                    await db.DeleteAccount(account.Name);
                }

                return CreateNoContentResponse();
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accounts/{accountName}")]
        [HttpGet()]
        public async Task<HttpResponseMessage> Get(string accountName)
        {
            try
            {
                Account key = null;

                using (var db = new Database())
                {
                    await ThrowIfNotAValidAccount(db, accountName);
                    key = await db.GetAccount(accountName);
                    key.AccessKeys.AddRange(await db.GetAccessKeys(accountName));
                }

                return CreateOKResponse(key);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskeys/wellknown")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetWellknownAccessKeys()
        {
            try
            {
                IEnumerable<AccessKey> keys = null;

                using (var db = new Database())
                {
                    keys = await db.GetAccessKeys();
                    foreach (var k in keys)
                        k.Workspace = await db.GetWorkspace(await db.GetWorkspaceIdByAccessKey(k.Id));
                }

                return CreateOKResponse(keys.Where(x => x.IsWellknown));
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskey/{accessKeyId}")]
        [HttpGet()]
        public async Task<HttpResponseMessage> Get(Guid accessKeyId)
        {
            try
            {
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKeyId);

                Workspace workspace = null;
                using (var db = new Database())
                {
                    workspace = await db.GetWorkspace(workspaceId);
                }

                //RemoveSecretInformationIfLowerPermissions(workspace, accessKeyId);
                return CreateOKResponse(workspace);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskeys")]
        [HttpPost()]
        public async Task<HttpResponseMessage> Post([FromBody]AccessKey accessKey)
        {
            try
            {
                AccessKey.Validate(accessKey);
                //            ThrowIfAccessKeyNotEqualWorkspace(accesskey, workspace);

                if (accessKey.Workspace == null)
                    accessKey.Workspace = new Workspace()
                    {
                        Name = Utility.GetRandomHashString()
                    };


                using (var db = new Database())
                {
                    await db.SaveAccessKey(accessKey);
                    await db.SaveWorkspace(accessKey.Workspace);
                }

                return CreateCreatedResponse(accessKey);
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
        /// <param name="workspaceAccessKeyId"></param>
        /// <param name="workspace"></param>
        /// <returns></returns>
        [Route("api/v1/accesskeys")]
        [HttpPut()]
        public async Task<HttpResponseMessage> Put(AccessKey accessKey)
        {
            try
            {
                AccessKey.Validate(accessKey);
                Workspace.Validate(accessKey.Workspace);
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKey.Id);

                using (var db = new Database())
                {
                    await ThrowIfNoWorkspaceAccess(db, accessKey.Workspace.WorkspaceId, accessKey.AccountName);
                    await db.SaveAccessKey(accessKey);
                    await db.SaveWorkspace(accessKey.Workspace);
                }

                return CreateNoContentResponse();
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskeys")]
        [HttpDelete()]
        public async Task<HttpResponseMessage> Delete(AccessKey accessKey)
        {
            try
            {
                using (var db = new Database())
                {
                    await db.DeleteWorkspaceKey(accessKey.Id);
                }

                return CreateNoContentResponse();
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskey/{accessKeyId}/results")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetResultsByAccessKey(Guid accessKeyId, long fromSequence = 0, int count = 30)
        {
            try
            {
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKeyId);
                return await GetResultsByWorkspace(workspaceId, fromSequence, count);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskey/{accessKeyId}/supportedFilters")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetWorkspaceSupportedFiltersByAccessKey(Guid accessKeyId)
        {
            try
            {
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKeyId);
                return GetWorkspaceSupportedFilters(workspaceId);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskey/{accessKeyId}/results/random")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetRandomResultsByAccessKey(Guid accessKeyId, int count = 1)
        {
            try
            {
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKeyId);

                return await GetRandomResultsByWorkspace(workspaceId, count);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskey/{accessKeyId}/results")]
        [HttpPost()]
        [HttpPut()]
        public async Task<HttpResponseMessage> PostResultsByAccessKey(Guid accessKeyId, [FromBody]IEnumerable<WorkspaceResult> results)
        {
            try
            {
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKeyId);

                return await PostResultsByWorkspace(workspaceId, results);
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
        /// <param name="workspaceId">Workspace guid</param>
        /// <param name="fromSequence">Minimum sequence value to start from</param>
        /// <param name="count">Max number of results to get</param>
        /// <returns>An enumerable array of results</returns>
        [Route("api/v1/workspaces/{workspaceId}/results")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetResultsByWorkspace(Guid workspaceId, long fromSequence = 0, int count = 30)
        {
            try
            {
                count = count.RangeConstraint(1, 50);
                fromSequence = fromSequence.MinConstraint(0);

                IEnumerable<WorkspaceResult> results = null;
                using (var db = new Database())
                    results = await db.GetWorkspaceResults(workspaceId, fromSequence, count);
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
        /// <param name="workspaceId">Workspace guid</param>
        /// <param name="minSequence">Minimum sequence value to start from</param>
        /// <param name="count">Max number of results to get</param>
        /// <returns>An enumerable array of results</returns>
        [Route("api/v1/workspaces/{workspaceId}/results/random")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetRandomResultsByWorkspace(Guid workspaceId, int count = 1)
        {
            try
            {
                count = count.RangeConstraint(1, 50);

                IEnumerable<WorkspaceResult> results = null;
                using (var db = new Database())
                    results = await db.GetWorkspaceResultsByRandom(workspaceId, count);
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
        /// <param name="workspaceId"></param>
        /// <param name="results"></param>
        [Route("api/v1/workspaces/{workspaceId}/results")]
        [HttpPost()]
        [HttpPut()]
        public async Task<HttpResponseMessage> PostResultsByWorkspace(Guid workspaceId, [FromBody]IEnumerable<WorkspaceResult> results)
        {
            try
            {
                using (var db = new Database())
                    await db.AddWorkspaceResults(workspaceId, results);

                return CreateCreatedResponse((WorkspaceResult)null);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        /// <summary>
        /// Delete some results from a workspace
        /// </summary>
        /// <param name="workspaceId"></param>
        /// <param name="results"></param>
        [Route("api/v1/workspaces/{workspaceId}/results")]
        [HttpDelete()]
        public HttpResponseMessage DeleteResultsByWorkspace(Guid workspaceId, [FromBody]IEnumerable<WorkspaceResult> results)
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

        [Route("api/v1/workspaces/{workspaceId}/supportedFilters")]
        [HttpGet()]
        public HttpResponseMessage GetWorkspaceSupportedFilters(Guid workspaceId)
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

                    return CreateOKResponse(results.Where(x => x.IsWellknown));
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

        private async Task ThrowIfNotAValidAccount(Database db, string accesskey)
        {
            if (!await db.IsValidAccountName(accesskey))
                throw new PermissionDeniedFetchoException("invalid key");
        }

        private async Task ThrowIfAccountExists(Database db, string accesskey)
        {
            if (await db.IsValidAccountName(accesskey))
                throw new PermissionDeniedFetchoException("bad key");
            if (accesskey.ToLower() == "wellknown")
                throw new PermissionDeniedFetchoException("bad key: reserved");
        }

        private void FixAccountObject(Account accessKey)
        {
            if (accessKey.Created == DateTime.MinValue)
                accessKey.Created = DateTime.Now;
        }

        private async Task<Guid> GetWorkspaceIdOrThrowIfNoAccess(Guid workspaceAccessKeyId)
        {
            using (var db = new Database())
                return await db.GetWorkspaceIdByAccessKey(workspaceAccessKeyId);
        }

        private HttpResponseMessage CreatePermissionDeniedResponse(PermissionDeniedFetchoException ex)
            => Request.CreateErrorResponse(HttpStatusCode.Forbidden, ex.Message, ex);

        private HttpResponseMessage CreateCreatedResponse<T>(T obj = default(T))
            => !EqualityComparer<T>.Default.Equals(obj, default(T)) ? Request.CreateResponse(HttpStatusCode.Created, obj) : Request.CreateResponse(HttpStatusCode.Created);

        private HttpResponseMessage CreateNoContentResponse()
            => Request.CreateResponse(HttpStatusCode.NoContent);

        private HttpResponseMessage CreateNotImplementedResponse(NotImplementedException ex)
            => Request.CreateResponse(HttpStatusCode.NotImplemented);

        private HttpResponseMessage CreateInvalidRequestResponse(InvalidRequestFetchoException ex)
            => Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex.Message, ex);


        private HttpResponseMessage CreateOKResponse<T>(T payload)
        {
            var resp = Request.CreateResponse(HttpStatusCode.OK, payload, ContentType.ApplicationJson);
            return resp;
        }

        private HttpResponseMessage CreateExceptionResponse(Exception ex)
        {
            // if something goes wrong in unit tests just throw the exception
            Utility.LogException(ex);
            if (Request == null) throw ex;
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
