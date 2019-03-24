using Fetcho.Common;
using Fetcho.Common.Entities;
using Fetcho.ContentReaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;

namespace Fetcho.FetchoAPI.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class WorkspacesController : ApiController
    {
        public const int MaxResultsReturned = 200;

        public WorkspacesController()
        {
            log4net.Config.XmlConfigurator.Configure();
        }

        #region Accounts

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
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accounts/{accountName}")]
        [HttpDelete()]
        public async Task<HttpResponseMessage> DeleteAccount(string accountName)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(accountName))
                    throw new InvalidRequestFetchoException("No object passed");

                using (var db = new Database())
                {
                    await ThrowIfNotAValidAccount(db, accountName);
                    var account = await db.GetAccount(accountName);
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
        public async Task<HttpResponseMessage> GetAccount(string accountName)
        {
            try
            {
                Account key = null;

                using (var db = new Database())
                {
                    key = await db.GetAccount(accountName);

                    if (key == null)
                        return Create404Response(key);

                    key.AccessKeys.AddRange(await db.GetAccessKeys(accountName, true));
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

        #endregion

        #region Access Keys

        [Route("api/v1/accesskey/{accessKeyId}")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetAccessKey(Guid accessKeyId)
        {
            try
            {
                AccessKey key = null;
                using (var db = new Database())
                {
                    key = await db.GetAccessKey(accessKeyId);

                    if (key == null)
                        return Create404Response(key);
                }

                if (key.HasPermissionFlags(WorkspaceAccessPermissions.Manage | WorkspaceAccessPermissions.Owner))
                {
                    key.Workspace.WorkspaceId = null;
                    key.Workspace.AccessKeys.Clear();
                }

                if (!key.HasPermissionFlags(WorkspaceAccessPermissions.Read))
                {
                    key.Workspace = null;
                }

                return CreateOKResponse(key);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskeys")]
        [HttpPost()]
        public async Task<HttpResponseMessage> PostAccessKey([FromBody]AccessKey accessKey)
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
        public async Task<HttpResponseMessage> PutAccessKey(AccessKey accessKey)
        {
            try
            {
                AccessKey.Validate(accessKey);
                Workspace.Validate(accessKey.Workspace);
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKey.Id);

                using (var db = new Database())
                {
                    await ThrowIfNotValidPermission(db, accessKey.Id, WorkspaceAccessPermissions.Owner | WorkspaceAccessPermissions.Manage);
                    await ThrowIfNoWorkspaceAccess(db, accessKey.Workspace.WorkspaceId.GetValueOrDefault(), accessKey.AccountName);
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

        [Route("api/v1/accesskey/{accessKeyId}")]
        [HttpDelete()]
        public async Task<HttpResponseMessage> DeleteAccessKey(Guid accessKeyId)
        {
            try
            {
                using (var db = new Database())
                {
                    await db.DeleteWorkspaceKey(accessKeyId);
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
        public async Task<HttpResponseMessage> GetWorkspaceResultsByAccessKeyId(Guid accessKeyId, long offset = 0, int count = MaxResultsReturned, string order = "ASC")
        {
            try
            {
                using (var db = new Database())
                    await ThrowIfNotValidPermission(db, accessKeyId, WorkspaceAccessPermissions.Read | WorkspaceAccessPermissions.Owner | WorkspaceAccessPermissions.Manage);
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKeyId);
                return await GetWorkspaceResultsByWorkspaceId(workspaceId, offset, count, order);
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
                using (var db = new Database())
                    await ThrowIfNotValidPermission(db, accessKeyId, WorkspaceAccessPermissions.Read | WorkspaceAccessPermissions.Owner);
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
        public async Task<HttpResponseMessage> GetRandomWorkspaceResultsByAccessKeyId(Guid accessKeyId, int count = 1)
        {
            try
            {
                using (var db = new Database())
                    await ThrowIfNotValidPermission(db, accessKeyId, WorkspaceAccessPermissions.Owner | WorkspaceAccessPermissions.Manage | WorkspaceAccessPermissions.Read);

                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKeyId);

                return await GetRandomResultsByWorkspaceId(workspaceId, count);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskey/{accessKeyId}/results")]
        [HttpPost()]
        [HttpPut()]
        public async Task<HttpResponseMessage> PostWorkspaceResultsByAccessKeyId(Guid accessKeyId, [FromBody]IEnumerable<WorkspaceResult> results)
        {
            try
            {
                using (var db = new Database())
                    await ThrowIfNotValidPermission(
                        db,
                        accessKeyId,
                        WorkspaceAccessPermissions.Write | WorkspaceAccessPermissions.Manage | WorkspaceAccessPermissions.Owner);
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKeyId);

                return await PostWorkspaceResultsByWorkspaceId(workspaceId, results);
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
        /// <param name="offset">Minimum sequence value to start from</param>
        /// <param name="count">Max number of results to get</param>
        /// <returns>An enumerable array of results</returns>
        [Route("api/v1/workspaces/{workspaceId}/results")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetWorkspaceResultsByWorkspaceId(Guid workspaceId, long offset = 0, int count = MaxResultsReturned, string order = "ASC")
        {
            try
            {
                bool descendingOrder = (order.ToLower() == "desc");
                count = count.RangeConstraint(1, MaxResultsReturned);
                offset = offset.MinConstraint(0);

                IEnumerable<WorkspaceResult> results = null;
                using (var db = new Database())
                    results = await db.GetWorkspaceResults(workspaceId, offset, count, descendingOrder);
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
        public async Task<HttpResponseMessage> GetRandomResultsByWorkspaceId(Guid workspaceId, int count = 1)
        {
            try
            {
                count = count.RangeConstraint(1, MaxResultsReturned);

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
        public async Task<HttpResponseMessage> PostWorkspaceResultsByWorkspaceId(Guid workspaceId, [FromBody]IEnumerable<WorkspaceResult> results)
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
        public HttpResponseMessage DeleteWorkspaceResultsByWorkspaceId(Guid workspaceId, [FromBody]IEnumerable<WorkspaceResult> results)
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

                var l = new List<FilterHelpInfo>();
                foreach (var filterType in filterTypes)
                {
                    var attr = filterType.GetCustomAttributes(typeof(FilterAttribute), false).FirstOrDefault() as FilterAttribute;
                    if (attr != null && !attr.Hidden)
                    {
                        l.Add(new FilterHelpInfo
                        {
                            TokenMatch = attr.TokenMatch,
                            ShortHelp = attr.ShortHelp,
                            Name = attr.Name,
                            Description = attr.Description
                        });

                    }
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

        [Route("api/v1/resources/")]
        [HttpPost()]
        [HttpPut()]
        public async Task<HttpResponseMessage> PostWebResourceDataCache([FromBody]Stream data)
        {
            MD5Hash hash = null;

            try
            {
                using (var ms = new MemoryStream())
                {
                    data.CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    hash = MD5Hash.Compute(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    using (var db = new Database())
                        await db.AddWebResourceDataCache(hash, ms);
                }

                return CreateCreatedResponse((Object)null);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/resources/{datahash}")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetWebResourceCacheData(string datahash)
        {
            try
            {
                using (var db = new Database())
                {
                    byte[] bytes = await db.GetWebResourceCacheData(new MD5Hash(datahash));

                    if (bytes == null)
                        return Create404Response((Object)null);

                    var result = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(bytes)
                    };
                    result.Content.Headers.ContentType =
                        new MediaTypeHeaderValue(ContentType.ApplicationOctetStream);

                    return result;
                }
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/resources/{datahash}/text")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetWebResourceCacheDataText(string datahash)
        {
            try
            {
                using (var db = new Database())
                {
                    byte[] bytes = await db.GetWebResourceCacheData(new MD5Hash(datahash));

                    if (bytes == null)
                        return Create404Response((Object)null);

                    var l = new List<string>();
                    using (var ms = new MemoryStream(bytes))
                    {
                        var parser = new BracketPipeTextExtractor
                        {
                            Distinct = true,
                            Granularity = ExtractionGranularity.Raw,
                            MaximumLength = int.MaxValue,
                            MinimumLength = int.MinValue,
                            StopWords = false
                        };
                        parser.Parse(ms, l.Add);
                    }

                    return CreateOKResponse(l);
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

        private async Task ThrowIfNotValidPermission(Database db, Guid accessKeyId, WorkspaceAccessPermissions permissionFlag)
        {
            var key = await db.GetAccessKey(accessKeyId);
            if (key == null)
                throw new AccessKeyDoesntExistFetchoException();
            if (!key.HasPermissionFlags(permissionFlag))
                throw new PermissionDeniedFetchoException("Permission denied");
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
            => CreateOKResponse<T>(payload, ContentType.ApplicationJson);

        private HttpResponseMessage CreateOKResponse<T>(T payload, string contentType)
            => Request.CreateResponse(HttpStatusCode.OK, payload, contentType);

        private HttpResponseMessage Create404Response<T>(T payload)
            => !EqualityComparer<T>.Default.Equals(payload, default(T)) ? Request.CreateResponse(HttpStatusCode.NotFound, payload) : Request.CreateResponse(HttpStatusCode.NotFound);

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
            if (ex is AccessKeyDoesntExistFetchoException doesntExistFetchoException)
                return Create404Response<Guid>(Guid.Empty);
            return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message, ex);
        }

        #endregion

    }
}
