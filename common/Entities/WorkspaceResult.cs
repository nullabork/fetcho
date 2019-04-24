using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

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

        public string[] Features { get; set; }

        public Guid SourceServerId { get; set; }

        [JsonIgnore]
        public Dictionary<string, string> RequestProperties { get; set; }

        [JsonIgnore]
        public Dictionary<string, string> ResponseProperties { get; set; }

        [JsonIgnore]
        public Dictionary<string, object> PropertyCache { get; set; }

        public WorkspaceResult()
        {
            Tags = new List<string>();
            PageSize = null;
            GlobalSequence = -1;
            Created = DateTime.MinValue;
            Updated = DateTime.MinValue;
            Description = string.Empty;
            Title = string.Empty;
            Uri = string.Empty;
            RefererUri = string.Empty;
            UriHash = string.Empty;
            DataHash = string.Empty;
            Features = new string[0];
            SourceServerId = Guid.Empty;

            RequestProperties = new Dictionary<string, string>();
            ResponseProperties = new Dictionary<string, string>();
            PropertyCache = new Dictionary<string, object>();
        }

        public string GetTagString()
            => Tags.Aggregate(string.Empty, (interim, next) => (interim + " " + next).Trim());

    }
}