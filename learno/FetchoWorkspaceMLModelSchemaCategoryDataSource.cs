using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fetcho.Common;
using Fetcho.Common.Entities;
using Fetcho.ContentReaders;

namespace learno
{
    public class FetchoWorkspaceMLModelSchemaCategoryDataSource : MLModelSchemaCategoryDataSource
    {
        public Guid WorkspaceId { get; set; }

        public bool UseNameForCategory { get; set; }

        public virtual BracketPipeTextFragment FilterRawTextFragments(BracketPipeTextFragment fragment)
            => fragment;

        public FetchoWorkspaceMLModelSchemaCategoryDataSource(string category, Guid workspaceId)
        {
            WorkspaceId = workspaceId;
            Name = category;
        }

        public override IEnumerable<TextPageData> GetData(int maxRecords)
        {
            var l = new List<TextPageData>();
            using (var db = new Database())
            {
                var results = db.GetWorkspaceResults(WorkspaceId, -1, -1).GetAwaiter().GetResult();
                l.AddRange(GetPages(db, results));
            }
            return l;
        }

        private string MakeCategory(WorkspaceResult result)
        {
            string tags = result.Tags.FirstOrDefault();
            if (String.IsNullOrWhiteSpace(tags) || UseNameForCategory)
                return Name;
            return tags;
        }

        private IEnumerable<TextPageData> GetPages(Database db, IEnumerable<WorkspaceResult> results)
        {
            var parser = new BracketPipeTextExtractor();

            var l = new List<TextPageData>();

            foreach (var result in results)
            {
                if (string.IsNullOrWhiteSpace(result.DataHash)) continue;
                var data = db.GetWebResourceCacheData(new MD5Hash(result.DataHash)).GetAwaiter().GetResult();

                if (data == null) continue;

                var t = new List<BracketPipeTextFragment>();
                using (var ms = new MemoryStream(data))
                    parser.Parse(ms, (x) => { var y = FilterRawTextFragments(x); t.AddIfNotNull(y); });

                l.Add(new TextPageData
                {
                    TextData = t.Aggregate("", (x, y) => string.Format("{0} {1} {2}", x, y.Tag, y.Text)),
                    Category = MakeCategory(result)
                });
            }

            return l;
        }
    }
}
