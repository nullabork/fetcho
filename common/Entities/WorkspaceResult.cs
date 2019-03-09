using System;
using System.Collections.Generic;
using System.Linq;

namespace Fetcho.Common.Entities
{
    public class WorkspaceResult
    {
        public string Hash { get; set; }

        public string RefererUri { get; set; }

        public string Uri { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public List<string> Tags { get;  }

        public DateTime Created { get; set; }

        public long PageSize { get; set; }

        public long Sequence { get; set; }

        public WorkspaceResult()
        {
            Tags = new List<string>();
            PageSize = -1;
            Sequence = -1;
            Created = DateTime.MinValue;
            Description = "";
            Title = "";
            Uri = "";
            RefererUri = "";
            Hash = "";
        }

        public string GetTagString()
            => Tags.Aggregate("", (interim, next) => (interim + " " + next).Trim());

    }
}