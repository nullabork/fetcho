using System;
using System.Collections.Generic;

namespace Fetcho.Common.Entities
{
    public class WorkspaceResult
    {
        public string Hash { get; set; }

        public string ReferrerUri { get; set; }

        public string Uri { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public List<string> Tags { get;  }

        public DateTime Created { get; set; }

        public long Size { get; set; }

        public long Sequence { get; set; }

        public WorkspaceResult()
        {
            Tags = new List<string>();
            Size = -1;
            Sequence = -1;
            Created = DateTime.MinValue;
            Description = "";
            Title = "";
            Uri = "";
            ReferrerUri = "";
            Hash = "";
        }
    }
}