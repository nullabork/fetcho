using System;
using System.IO;
using System.Linq;
using Fetcho.Common.Entities;
using Fetcho.Common.QueryEngine;

namespace Fetcho.Common
{
    [Filter("query(", "query(access_key_id):[search text|*][:search text|*]")]
    public class WorkspaceSubQueryFilter : Filter, ISubQuery
    {
        const int SubQueryMaxDepth = 10;

        const string WorkspaceQueryFilterKey = "query(";

        public string SearchText { get; set; }

        public string HeaderKey { get; set; }

        public Query Query { get; set; }

        public override string Name => "Workspace Sub Query Filter";

        public override decimal Cost
            => Query.AvgCost < 0 ?
            Query.IncludeFilters.Sum(x => x.Cost) + Query.ExcludeFilters.Sum(x => x.Cost) + Query.TagFilters.Sum(x => x.Cost) :
            Query.AvgCost;

        public override bool RequiresResultInput { get => Query.RequiresResultInput; }
        public override bool RequiresStreamInput { get => Query.RequiresStreamInput; }
        public override bool RequiresTextInput { get => Query.RequiresTextInput; }

        public override bool IsReducingFilter => true;

        public override bool IsSubQuery => true;

        public WorkspaceSubQueryFilter(Query query, string headerKey, string searchText)
        {
            Query = query;
            SearchText = searchText.ToLower();
            HeaderKey = headerKey.ToLower();
        }

        public override string GetQueryText()
            => string.Format("{0}{1}):{2}", WorkspaceQueryFilterKey, HeaderKey, SearchText);

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            var eval = Query.Evaluate(result, fragment, stream);

            if (eval.Action == EvaluationResultAction.Include)
            {
                return eval.Tags.Any() ? result.Tags.ToArray() : new string[1];
            }

            return EmptySet;
        }

        /// <summary>
        /// Parse some text to create this object
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText, int depth)
        {
            if (depth > SubQueryMaxDepth)
                throw new InvalidObjectFetchoException("Sub query max depth reached");

            string searchText = String.Empty;

            var tokens = queryText.Split(':');
            if (tokens.Length != 2) return null;

            var key = tokens[0].Substring(WorkspaceQueryFilterKey.Length, tokens[0].Length - WorkspaceQueryFilterKey.Length - 1);
            searchText = tokens[1].Trim();

            if (Guid.TryParse(key, out Guid accessKeyId))
            {
                AccessKey accessKey = null;
                using (var db = new Database())
                {
                    accessKey = db.GetAccessKey(accessKeyId).GetAwaiter().GetResult();
                    if (accessKey != null)
                        return new WorkspaceSubQueryFilter(new Query(accessKey.Workspace.QueryText, depth + 1), key, searchText);
                }
            }

            return null;
        }
    }
}
