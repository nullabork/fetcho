using Fetcho.Common;
using Fetcho.Common.Entities;
using Fetcho.Common.QueryEngine;
using Fetcho.ContentReaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;

namespace Fetcho.FetchoAPI.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public partial class WorkspacesController : ApiController
    {
        public const int MaxResultsReturned = 1000;

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
                Account acct = null;

                using (var db = new Database())
                {
                    acct = await db.GetAccount(accountName);

                    if (acct == null)
                        return Create404Response(accountName);

                    acct.AccessKeys.AddRange(await db.GetAccessKeys(accountName, true));
                    acct.Properties.AddRange(await db.GetAccountProperties(accountName));
                }

                return CreateOKResponse(acct);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accounts/{accountName}/properties")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetAccountProperties(string accountName)
        {
            try
            {
                IEnumerable<AccountProperty> props = null;

                using (var db = new Database())
                {
                    await ThrowIfNotAValidAccount(db, accountName);
                    props = await db.GetAccountProperties(accountName);
                }

                return CreateOKResponse(props);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accounts/{accountName}/properties")]
        [HttpPost()]
        public async Task<HttpResponseMessage> SetAccountProperty(string accountName, [FromBody]AccountProperty property)
        {
            try
            {
                using (var db = new Database())
                {
                    await ThrowIfNotAValidAccount(db, accountName);
                    await db.SetAccountProperty(accountName, property.Key, property.Value);
                }

                return CreateNoContentResponse();
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accounts/{accountName}/properties")]
        [HttpDelete()]
        public async Task<HttpResponseMessage> DeleteAccountProperty(string accountName, [FromBody]AccountProperty property)
        {
            try
            {
                using (var db = new Database())
                {
                    await ThrowIfNotAValidAccount(db, accountName);
                    await db.SetAccountProperty(accountName, property.Key, String.Empty);
                }

                return CreateNoContentResponse();
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accounts/{accountName}/stats")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetAccountStats(string accountName, string workspaceId = "", int offset = 0, int limit = 1)
        {
            try
            {
                offset = offset.ConstrainMin(0);
                limit = limit.ConstrainRange(1, 250);
                Guid.TryParse(workspaceId, out Guid guid);

                using (var db = new Database())
                {
                    await ThrowIfNotAValidAccount(db, accountName);
                    var results = await db.GetWorkspaceQueryStatsByAccount(accountName, guid, offset, limit);
                    return CreateOKResponse(results);
                }
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

                if (!key.HasPermissionFlags(WorkspaceAccessPermissions.Read | WorkspaceAccessPermissions.Manage | WorkspaceAccessPermissions.Owner))
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

                if (accessKey.Workspace == null)
                {
                    accessKey.Workspace = new Workspace()
                    {
                        Name = Utility.GetRandomHashString()
                    };

                    accessKey.Permissions = WorkspaceAccessPermissions.Owner;
                }

                using (var db = new Database())
                {
                    accessKey.Revision++;
                    accessKey.Workspace.Revision++;

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

        [Route("api/v1/accesskey/{writeAccessKeyId}")]
        [HttpPatch()]
        public async Task<HttpResponseMessage> PatchAccessKey(Guid writeAccessKeyId, [FromBody]dynamic delta)
        {
            int workspaceRevision = 0;
            int accessKeyRevision = 0;

            try
            {
                using (var db = new Database())
                {
                    var accessKey = await db.GetAccessKey(writeAccessKeyId); // TODO: Change this to delta.Id when someone realises you can't write to read-only keys issue comes up
                    if (accessKey == null)
                        throw new AccessKeyDoesntExistFetchoException("AccessKey doesnt exist");

                    // gotta pass the accesskeyid of that has the capability to use the delta
                    await ThrowIfNotValidPermission(db, writeAccessKeyId, WorkspaceAccessPermissions.Manage | WorkspaceAccessPermissions.Owner);

                    if (accessKey.Workspace == null)
                        accessKey.Workspace = new Workspace()
                        {
                            Name = Utility.GetRandomHashString()
                        };

                    if (delta["Workspace"] != null)
                    {
                        if (delta["Workspace"]["QueryText"] != null)
                            accessKey.Workspace.QueryText = delta.Workspace.QueryText;
                        if (delta["Workspace"]["Name"] != null)
                            accessKey.Workspace.Name = delta.Workspace.Name;
                        if (delta["Workspace"]["Description"] != null)
                            accessKey.Workspace.Description = delta.Workspace.Description;
                        if (delta["Workspace"]["IsActive"] != null)
                            accessKey.Workspace.IsActive = delta.Workspace.IsActive;
                        if (delta["Workspace"]["IsWellknown"] != null)
                            accessKey.Workspace.IsWellknown = delta.Workspace.IsWellknown;

                        if (delta["Workspace"]["Revision"] != null)
                            workspaceRevision = delta.Workspace.Revision;
                        if (delta["Revision"] != null)
                            accessKeyRevision = delta.Revision;
                    }

                    accessKey.Workspace.AccessKeys.Clear();

                    AccessKey.Validate(accessKey);

                    try
                    {
                        ThrowIfAccessKeyRevisionNumberIsNotEqual(accessKey, accessKeyRevision);
                        ThrowIfWorkspaceRevisionNumberIsNotEqual(accessKey.Workspace, workspaceRevision);
                    }
                    catch (DataConcurrencyFetchoException)
                    {
                        return CreateConflictResponse(accessKey);
                    }

                    accessKey.Revision++;
                    accessKey.Workspace.Revision++;

                    await db.SaveAccessKey(accessKey);
                    await db.SaveWorkspace(accessKey.Workspace);

                    return CreateUpdatedResponse(accessKey);
                }
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
        public async Task<HttpResponseMessage> GetWorkspaceResultsByAccessKeyId(Guid accessKeyId, long offset = 0, int count = MaxResultsReturned, string order = "sequence:asc", string query = "")
        {
            try
            {
                using (var db = new Database())
                    await ThrowIfNotValidPermission(db, accessKeyId, WorkspaceAccessPermissions.Read | WorkspaceAccessPermissions.Owner | WorkspaceAccessPermissions.Manage);
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKeyId);
                return await GetWorkspaceResultsByWorkspaceId(workspaceId, offset, count, order, query);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        [Route("api/v1/accesskey/{accessKeyId}/results/social")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetWorkspaceResultsByAccessKeyIdFormatedForSocial(Guid accessKeyId, long offset = 0, int count = MaxResultsReturned, string order = "sequence:asc")
        {
            try
            {
                using (var db = new Database())
                    await ThrowIfNotValidPermission(db, accessKeyId, WorkspaceAccessPermissions.Read | WorkspaceAccessPermissions.Owner | WorkspaceAccessPermissions.Manage);
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKeyId);
                return await GetWorkspaceResultsByWorkspaceIdFormatedForSocial(workspaceId, offset, count, order);
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

                return await GetRandomWorkspaceResultsByWorkspaceId(workspaceId, count);
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

        [Route("api/v1/accesskey/{accessKeyId}/results/transform")]
        [HttpPut()]
        public async Task<HttpResponseMessage> TransformWorkspaceResultsByAccessKeyId(Guid accessKeyId, [FromBody]WorkspaceResultTransform transform)
        {
            try
            {
                using (var db = new Database())
                    await ThrowIfNotValidPermission(
                        db,
                        accessKeyId,
                        WorkspaceAccessPermissions.Write | WorkspaceAccessPermissions.Manage | WorkspaceAccessPermissions.Owner);
                Guid workspaceId = await GetWorkspaceIdOrThrowIfNoAccess(accessKeyId);

                return await TransformWorkspaceResultsByWorkspaceId(workspaceId, transform);
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
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
        public async Task<HttpResponseMessage> GetWorkspaceResultsByWorkspaceId(Guid workspaceId, long offset = 0, int count = MaxResultsReturned, string order = "sequence:asc", string query = "")
        {
            IEnumerable<WorkspaceResult> results = null;
            string[] acceptableOrderByTokens = { "sequence", "updated", "asc", "desc" };

            try
            {
                count = count.ConstrainRange(1, MaxResultsReturned);
                offset = offset.ConstrainMin(0);
                ThrowIfOrderParameterIsInvalid(acceptableOrderByTokens, order);

                Query qry = null;
                if (!String.IsNullOrWhiteSpace(query))
                {
                    qry = new Query(query);
                    ThrowIfQueryContainsInvalidOptions(qry);

                    var r = new WorkspaceResultResponse();
                    using (var db = new Database())
                    {
                        results = await db.GetWorkspaceResults(workspaceId, -1, -1, BuildSqlOrderByStringFromQueryStringOrderString(order));
                        var distilled = qry.Distill(results);
                        r.Results = distilled.Skip((int)offset).Take(count);
                        r.QueryText = query;
                        r.TotalWorkspaceResults = results.Count();
                        r.PageSize = Math.Min(r.Results.Count(), count);
                        r.PageNumber = (long)Math.Floor((decimal)offset/count);
                        r.SubsetCount = distilled.Count();
                    }

                    return CreateOKResponse(r);
                }
                else // remove this once the new looko is released
                {
                    using (var db = new Database())
                        results = await db.GetWorkspaceResults(workspaceId, offset, count, BuildSqlOrderByStringFromQueryStringOrderString(order));

                    return CreateOKResponse(results);
                }
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(ex);
            }
        }

        /// <summary>
        /// Get some results from a workspace
        /// </summary>
        /// <param name="workspaceId">Workspace guid</param>
        /// <param name="offset">Minimum sequence value to start from</param>
        /// <param name="count">Max number of results to get</param>
        /// <returns>An enumerable array of results</returns>
        [Route("api/v1/workspaces/{workspaceId}/results/social")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetWorkspaceResultsByWorkspaceIdFormatedForSocial(Guid workspaceId, long offset = 0, int count = MaxResultsReturned, string order = "sequence:asc")
        {
            string[] acceptableOrderByTokens = { "sequence", "updated", "asc", "desc" };

            try
            {
                count = count.ConstrainRange(1, MaxResultsReturned);
                offset = offset.ConstrainMin(0);
                ThrowIfOrderParameterIsInvalid(acceptableOrderByTokens, order);

                using (var db = new Database())
                {

                    IEnumerable<WorkspaceResult> results = await db.GetWorkspaceResults(workspaceId, offset, count, BuildSqlOrderByStringFromQueryStringOrderString(order));

                    var sresults = WorkspaceResultSocialFormat.FromWorkspaceResults(results);
                    var builder = new WorkspaceResultBuilder();

                    foreach (var sresult in sresults)
                    {
                        var data = await db.GetWebResourceCacheData(new MD5Hash(sresult.DataHash));
                        if (data == null) continue;

                        using (var ms = new MemoryStream(data))
                        {
                            var t = builder.Build(ms, "", "content-type: text/html", out string s);

                            sresult.ImageUrl = t.PropertyCache.SafeGet("og_image")?.ToString();
                            sresult.Author = t.PropertyCache.SafeGet("og_author")?.ToString();
                            sresult.ResultType = t.PropertyCache.SafeGet("og_type")?.ToString();
                            sresult.SiteName = t.PropertyCache.SafeGet("og_site_name")?.ToString();
                        }
                    }

                    return CreateOKResponse(sresults);
                }
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
        /// <param name="count">Max number of results to get</param>
        /// <returns>An enumerable array of results</returns>
        [Route("api/v1/workspaces/{workspaceId}/results/random")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetRandomWorkspaceResultsByWorkspaceId(Guid workspaceId, int count = 1)
        {
            try
            {
                count = count.ConstrainRange(1, MaxResultsReturned);

                IEnumerable<WorkspaceResult> results = null;
                using (var db = new Database())
                    results = await db.GetRandomWorkspaceResultsByWorkspaceId(workspaceId, count);
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
                {
                    foreach (var result in results)
                    {
                        // update updated dates
                        var r = await db.GetWorkspaceResultByHash(new MD5Hash(result.UriHash));
                        if (r == null || r.DataHash != result.DataHash)
                            result.Updated = DateTime.UtcNow;
                        else
                            result.Updated = r.Updated;
                    }

                    await db.AddWorkspaceResults(workspaceId, results);
                }

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
        [Route("api/v1/workspaces/{workspaceId}/results/transform")]
        [HttpPut()]
        public async Task<HttpResponseMessage> TransformWorkspaceResultsByWorkspaceId(Guid workspaceId, [FromBody]WorkspaceResultTransform transform)
        {
            try
            {
                WorkspaceResultTransform.Validate(transform);

                using (var db = new Database())
                {
                    // do we have access to that target?
                    if (transform.HasTarget)
                        await ThrowIfNotValidPermission(
                            db,
                            transform.TargetAccessKeyId,
                            WorkspaceAccessPermissions.Write | WorkspaceAccessPermissions.Owner | WorkspaceAccessPermissions.Manage);

                    if (transform.Action == WorkspaceResultTransformAction.DeleteAll)
                    {
                        transform.ResultsAffected = await db.DeleteAllWorkspaceResultsByWorkspaceId(workspaceId);
                    }
                    else if (transform.Action == WorkspaceResultTransformAction.DeleteSpecificResults)
                    {
                        transform.ResultsAffected = await db.DeleteWorkspaceResultsByWorkspaceId(workspaceId, transform.Results);
                    }
                    else if (transform.Action == WorkspaceResultTransformAction.DeleteByQueryText)
                    {
                        var query = new Query(transform.QueryText);
                        ThrowIfQueryContainsInvalidOptions(query);
                        var results = await db.GetWorkspaceResults(workspaceId);
                        var resultsToDelete = query.Distill(results);
                        transform.ResultsAffected = await db.DeleteWorkspaceResultsByWorkspaceId(workspaceId, resultsToDelete);
                    }

                    else if (transform.Action == WorkspaceResultTransformAction.MoveAllTo)
                    {
                        var targetWorkspaceId = await GetWorkspaceIdOrThrowIfNoAccess(transform.TargetAccessKeyId);
                        await db.MoveAllWorkspaceResultsByWorkspaceId(workspaceId, targetWorkspaceId);
                    }
                    else if (transform.Action == WorkspaceResultTransformAction.MoveSpecificTo)
                    {
                        var targetWorkspaceId = await GetWorkspaceIdOrThrowIfNoAccess(transform.TargetAccessKeyId);
                        await db.MoveWorkspaceResultsByWorkspaceId(workspaceId, targetWorkspaceId, transform.Results);
                    }
                    else if (transform.Action == WorkspaceResultTransformAction.MoveByQueryText)
                    {
                        var targetWorkspaceId = await GetWorkspaceIdOrThrowIfNoAccess(transform.TargetAccessKeyId);

                        var query = new Query(transform.QueryText);
                        ThrowIfQueryContainsInvalidOptions(query);
                        var results = await db.GetWorkspaceResults(workspaceId);
                        var resultsToMove = query.Distill(results); 
                        transform.ResultsAffected = await db.MoveWorkspaceResultsByWorkspaceId(workspaceId, targetWorkspaceId, resultsToMove);
                    }

                    else if (transform.Action == WorkspaceResultTransformAction.CopyAllTo)
                    {
                        var targetWorkspaceId = await GetWorkspaceIdOrThrowIfNoAccess(transform.TargetAccessKeyId);
                        await db.MoveAllWorkspaceResultsByWorkspaceId(workspaceId, targetWorkspaceId);
                    }
                    else if (transform.Action == WorkspaceResultTransformAction.CopySpecificTo)
                    {
                        var targetWorkspaceId = await GetWorkspaceIdOrThrowIfNoAccess(transform.TargetAccessKeyId);
                        await db.MoveWorkspaceResultsByWorkspaceId(workspaceId, targetWorkspaceId, transform.Results);
                    }
                    else if (transform.Action == WorkspaceResultTransformAction.CopyByQueryText)
                    {
                        var targetWorkspaceId = await GetWorkspaceIdOrThrowIfNoAccess(transform.TargetAccessKeyId);

                        var query = new Query(transform.QueryText);
                        ThrowIfQueryContainsInvalidOptions(query);
                        var results = await db.GetWorkspaceResults(workspaceId);
                        var resultsToCopy = query.Distill(results);
                        transform.ResultsAffected = await db.MoveWorkspaceResultsByWorkspaceId(workspaceId, targetWorkspaceId, resultsToCopy);
                    }

                    else if (transform.Action == WorkspaceResultTransformAction.TagAll)
                    {
                        await db.TagAllWorkspaceResultsByWorkspaceId(workspaceId, transform.Tag);
                    }
                    else if (transform.Action == WorkspaceResultTransformAction.TagSpecificResults)
                    {
                        await db.TagWorkspaceResultsByWorkspaceId(workspaceId, transform.Results, transform.Tag);
                    }
                    else if (transform.Action == WorkspaceResultTransformAction.TagByQueryText)
                    {
                        var query = new Query(transform.QueryText);
                        ThrowIfQueryContainsInvalidOptions(query);
                        var results = await db.GetWorkspaceResults(workspaceId);
                        var resultsToTag = query.Distill(results);
                        transform.ResultsAffected = await db.TagWorkspaceResultsByWorkspaceId(workspaceId, resultsToTag, transform.Tag);
                    }

                    else if (transform.Action == WorkspaceResultTransformAction.UntagAll)
                    {
                        await db.UntagAllWorkspaceResultsByWorkspaceId(workspaceId, transform.Tag);
                    }
                    else if (transform.Action == WorkspaceResultTransformAction.UntagSpecificResults)
                    {
                        await db.UntagWorkspaceResultsByWorkspaceId(workspaceId, transform.Results, transform.Tag);
                    }
                    else if (transform.Action == WorkspaceResultTransformAction.UntagByQueryText)
                    {
                        var query = new Query(transform.QueryText);
                        ThrowIfQueryContainsInvalidOptions(query);
                        var results = await db.GetWorkspaceResults(workspaceId);
                        var resultsToTag = query.Distill(results);
                        transform.ResultsAffected = await db.UntagWorkspaceResultsByWorkspaceId(workspaceId, resultsToTag, transform.Tag);
                    }

                    transform.Success = true;

                    return CreateOKResponse(transform);
                }
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
                transform.ErrorMessage = ex.Message;
                transform.Success = false;
                transform.ResultsAffected = 0;
                return CreateConflictResponse(transform);
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
                        l.Add(new FilterHelpInfo(attr));
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
                        await db.AddWebResourceDataCache(hash, ms.GetBuffer());
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

        // info
        //   # keywords
        //   # content type, provided
        //   # content type, guessed
        //   # language provided
        //   # language guessed
        //   tags:
        //     ml-models matched
        //   
        //   paragraphs
        //   scripts
        //   raw text
        //   stop words
        //   last fetched
        //   links

        [Route("api/v1/resources/{datahash}/text")]
        [HttpGet()]
        public async Task<HttpResponseMessage> GetWebResourceCacheDataText(
            string datahash,
            BracketPipeTextExtractorFilterType filter = BracketPipeTextExtractorFilterType.Raw,
            int minlen = int.MinValue,
            int maxlen = int.MaxValue,
            bool distinct = true,
            bool stopWords = true,
            ExtractionGranularity granularity = ExtractionGranularity.Raw
            )
        {
            try
            {
                using (var db = new Database())
                {
                    byte[] bytes = await db.GetWebResourceCacheData(new MD5Hash(datahash));

                    if (bytes == null)
                        return Create404Response((Object)null);

                    var l = new List<BracketPipeTextFragment>();
                    using (var ms = new MemoryStream(bytes))
                    {
                        var parser = new BracketPipeTextExtractor
                        {
                            Distinct = distinct,
                            Granularity = granularity,
                            MaximumLength = maxlen,
                            MinimumLength = minlen,
                            StopWords = stopWords,
                            Filter = filter
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

        #region

        [Route("api/v1/parser/query")]
        [HttpPost()]
        public HttpResponseMessage GetQueryParserOutput([FromBody]JsonQuery jsonQuery)
        {
            try
            {
                var response = QueryParserResponse.Create(jsonQuery.QueryText);

                return CreateOKResponse(response);
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
                if (ex is ReflectionTypeLoadException rex)
                    Utility.LogException(rex.LoaderExceptions.First());
                return CreateExceptionResponse(ex);
            }
        }


        #endregion

        #region helper methods

        /// <summary>
        /// Take string in query string order format [field]:[sort] and convert it to SQL order by format
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private string BuildSqlOrderByStringFromQueryStringOrderString(string order)
            => order.Replace(':', ' ');

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

        private void ThrowIfAccessKeyRevisionNumberIsNotEqual(AccessKey key, int revision)
        {
            if (key.Revision != revision)
                throw new DataConcurrencyFetchoException("The revision number sent is older than the current database");
        }

        private void ThrowIfWorkspaceRevisionNumberIsNotEqual(Workspace key, int revision)
        {
            if (key.Revision != revision)
                throw new DataConcurrencyFetchoException("The revision number sent is older than the current database");
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
                throw new PermissionDeniedFetchoException("No access to {0} {1}", guid, accesskey);
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

        private void ThrowIfOrderParameterIsInvalid(string[] acceptableOrderByTokens, string order)
        {
            var orderTokens = order.Split(',', ':');
            if (orderTokens.Intersect(acceptableOrderByTokens).Count() != orderTokens.Length)
                throw new InvalidRequestFetchoException("Invalid query parameter: order");
        }

        private void ThrowIfNotADeleteTransform(WorkspaceResultTransform transform)
        {
            if (!(
                transform.Action == WorkspaceResultTransformAction.DeleteAll ||
                transform.Action == WorkspaceResultTransformAction.DeleteSpecificResults ||
                transform.Action == WorkspaceResultTransformAction.DeleteByQueryText))
                throw new InvalidRequestFetchoException("The transform passed through is not a delete action");
        }

        private void ThrowIfQueryContainsInvalidOptions(Query query)
        {
            if (query.RequiresStreamInput || query.RequiresTextInput)
                throw new InvalidRequestFetchoException("Query contains options which can't be used for this purpose");
        }

        private void FixAccountObject(Account accessKey)
        {
            if (accessKey.Created == DateTime.MinValue)
                accessKey.Created = DateTime.UtcNow;
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

        private HttpResponseMessage CreateConflictResponse<T>(T obj = default(T))
            => !EqualityComparer<T>.Default.Equals(obj, default(T)) ? Request.CreateResponse(HttpStatusCode.Conflict, obj) : Request.CreateResponse(HttpStatusCode.Conflict);

        private HttpResponseMessage CreateUpdatedResponse<T>(T obj = default(T))
            => !EqualityComparer<T>.Default.Equals(obj, default(T)) ? Request.CreateResponse(HttpStatusCode.Accepted, obj) : Request.CreateResponse(HttpStatusCode.Accepted);

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