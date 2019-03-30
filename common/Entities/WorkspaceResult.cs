using System;
using System.Collections.Generic;
using System.Linq;

namespace Fetcho.Common.Entities
{
    public class WorkspaceResult
    {
        public string UriHash { get; set; }

        public string RefererUri { get; set; }

        public string Uri { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public List<string> Tags { get;  }

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }

        public long? PageSize { get; set; }

        public long GlobalSequence { get; set; }

        public string DataHash { get; set; }

        public string DebugInfo { get; set; }

        public WorkspaceResult()
        {
            Tags = new List<string>();
            PageSize = null;
            GlobalSequence = -1;
            Created = DateTime.MinValue;
            Updated = DateTime.MinValue;
            Description = String.Empty;
            Title = String.Empty;
            Uri = String.Empty;
            RefererUri = String.Empty;
            UriHash = String.Empty;
            DataHash = String.Empty;
            DebugInfo = String.Empty;
        }

        public string GetTagString()
            => Tags.Aggregate(String.Empty, (interim, next) => (interim + " " + next).Trim());

    }
}