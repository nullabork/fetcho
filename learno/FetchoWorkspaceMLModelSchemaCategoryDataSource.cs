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

        public FetchoWorkspaceMLModelSchemaCategoryDataSource(string category, Guid workspaceId)
        {
            WorkspaceId = workspaceId;
            Category = category;
        }

        public override IEnumerable<TextPageData> GetData(int maxRecords)
        {
            var l = new List<TextPageData>();
            using (var db = new Database())
            {
                var results = db.GetRandomWorkspaceResultsByWorkspaceId(WorkspaceId, maxRecords).GetAwaiter().GetResult();
                l.AddRange(GetPages(db, Category, results));
            }
            return l;
        }

        private IEnumerable<TextPageData> GetPages(Database db, string label, IEnumerable<WorkspaceResult> results)
        {
            var parser = new BracketPipeTextExtractor();

            var l = new List<TextPageData>();

            foreach (var result in results)
            {
                if (string.IsNullOrWhiteSpace(result.DataHash)) continue;
                var data = db.GetWebResourceCacheData(new MD5Hash(result.DataHash)).GetAwaiter().GetResult();

                if (data == null) continue;

                var t = new List<string>();
                using (var ms = new MemoryStream(data))
                    parser.Parse(ms, t.Add);

                l.Add(new TextPageData
                {
                    TextData = t.Aggregate("", (x, y) => x + " " + y),
                    Category = label
                });
            }

            return l;
        }
    }
}
