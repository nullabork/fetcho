using Fetcho.Common.Entities;
using System.Collections.Generic;

namespace Fetcho.FetchoAPI.Controllers
{
    public class WorkspaceResultResponse
    {
        public IEnumerable<WorkspaceResult> Results { get; set; }
        public string QueryText { get; set; }
        public long SubsetCount { get; set; }
        public long TotalWorkspaceResults { get; set; }
        public long PageNumber { get; set; }
        public long PageSize { get; set; }
    }
}