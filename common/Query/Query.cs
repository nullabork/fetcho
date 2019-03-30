using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Fetcho.Common.Entities;

namespace Fetcho.Common.QueryEngine
{
    /// <summary>
    /// A collection of filters and how they operate together
    /// </summary>
    public class Query
    {
        private FilterCollection IncludeFilters = new FilterCollection();
        private FilterCollection ExcludeFilters = new FilterCollection();
        private FilterCollection TagFilters = new FilterCollection();

        public long MinCost { get; set; }
        public long MaxCost { get; set; }
        public long AvgCost { get => NumberOfEvaluations == 0 ? 0 : TotalCost / NumberOfEvaluations; }
        public long TotalCost { get; set; }
        public int NumberOfEvaluations { get; set; }
        public string OriginalQueryText { get; set; }

        public bool RequiresTextInput { get; protected set; }
        public bool RequiresStreamInput { get; protected set; }
        public bool RequiresResultInput { get; protected set; }

        public Query(string queryText)
        {
            OriginalQueryText = queryText;
            ParseQueryText(queryText);
            Console.WriteLine(this.ToString());
        }

        /// <summary>
        /// Evalute some resource against this query to figure out whether to ignore, include or exclude it from the workspace
        /// </summary>
        /// <param name="result"></param>
        /// <param name="words"></param>
        /// <param name="stream"></param>
        /// <returns></returns>
        public EvaluationResult Evaluate(WorkspaceResult result, string words, Stream stream)
        {
            IEnumerable<string> tags = null;
            var ticks = DateTime.UtcNow.Ticks;
            var action = EvaluationResultAction.NotEvaluated;

            var exc = ExcludeFilters.AnyMatch(result, words, stream);

            if (exc)
                action = EvaluationResultAction.Exclude;
            else
            {
                var inc = IncludeFilters.AllMatch(result, words, stream);

                if (inc) action = EvaluationResultAction.Include;
                else if (!inc) action = EvaluationResultAction.NotEvaluated;

                if (action == EvaluationResultAction.Include)
                    tags = TagFilters.GetTags(result, words, stream);
            }

            var r = new EvaluationResult(action, tags, DateTime.UtcNow.Ticks - ticks);
            DoBookKeeping(r);
            return r;
        }

        /// <summary>
        /// Parse query text to build the filter collections
        /// </summary>
        /// <param name="text">The query text</param>
        private void ParseQueryText(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return;
            string[] tokens = text.Split(' ');

            foreach (var token in tokens)
            {
                string filterToken = token;
                string tagToken = "";
                FilterMode filterMode = DetermineFilterMode(token);
                if (filterMode == FilterMode.None) continue;
                if ((filterMode & FilterMode.Exclude) == FilterMode.Exclude) filterToken = filterToken.Substring(1); // chop off the '-'
                if ((filterMode & FilterMode.Tag) == FilterMode.Tag)
                {
                    var ts = filterToken.Split(':');
                    tagToken = ts[0] + ":" + ts[2];
                    filterToken = filterToken.Substring(0, filterToken.LastIndexOf(':')); // remove the tag part
                }

                Filter filter = null;
                Filter tagger = null;
                if (!IsComplexFilter(filterToken))
                    filter = new TextMatchFilter(filterToken);
                else
                {
                    filter = Filter.CreateFilter(filterToken);
                    tagger = Filter.CreateFilter(tagToken);
                }

                switch (filterMode)
                {
                    case FilterMode.Include:
                        IncludeFilters.Add(filter);
                        break;

                    case FilterMode.Exclude:
                        ExcludeFilters.Add(filter);
                        break;

                    case FilterMode.Tag:
                        TagFilters.Add(filter);
                        break;

                    case (FilterMode.Tag | FilterMode.Include):
                        IncludeFilters.Add(filter);
                        TagFilters.Add(tagger);
                        break;

                    default:
                        Utility.LogInfo("Unknown filterMode {0}", filterMode);
                        break;
                }

            }
        }


        /// <summary>
        /// Is it more than just a word filter
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool IsComplexFilter(string token)
            => token.Contains(":");

        /// <summary>
        /// Determines if this token is a wildcard search
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool IsWildcardSearch(string token)
            => token.EndsWith(Filter.WildcardChar);

        /// <summary>
        /// Is this one for tagging?
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool IsTagFilter(string token)
            => token.Split(':').Length == 3;

        /// <summary>
        /// Returns true if this filter will only return a subset of the results passed to it - ie. its not just a wildcard
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool IsDiscriminatingFilter(string token)
        {
            var ts = token.Split(':');
            if (ts.Length < 2) return true;
            if (ts[1].Trim().Length == 0) return false;
            if (ts[1].Trim().Length == 1 && ts[1].Trim() == Filter.WildcardChar && !IsFunctionFilter(token)) return false;
            return true;
        }

        /// <summary>
        /// Returns true if this filter is a functional filter like ml-model
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool IsFunctionFilter(string token)
            => token.Contains("(") && token.Contains(")");

        /// <summary>
        /// Determine the type of filter being applied
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private FilterMode DetermineFilterMode(string token)
        {
            FilterMode rtn = FilterMode.None;

            if (token.StartsWith("-", StringComparison.InvariantCultureIgnoreCase))
                rtn = FilterMode.Exclude;
            else
            {
                if (IsDiscriminatingFilter(token)) rtn |= FilterMode.Include;
                if (IsTagFilter(token)) rtn |= FilterMode.Tag;
            }

            return rtn;
        }

        /// <summary>
        /// Calculate the cost of this query for optimisation and debugging purposes
        /// </summary>
        /// <param name="r"></param>
        private void DoBookKeeping(EvaluationResult r)
        {
            if (r.Cost < MinCost) MinCost = r.Cost;
            if (r.Cost > MaxCost) MaxCost = r.Cost;
            TotalCost += r.Cost;
            NumberOfEvaluations++;
        }

        /// <summary>
        /// Builds a string of the cost details of this query
        /// </summary>
        /// <returns></returns>
        public string CostDetails()
            => String.Format("Min: {0}, Max: {1}, Avg: {2}, Total: {3}, #: {4}",
                MinCost, MaxCost, AvgCost, TotalCost, NumberOfEvaluations);

        /// <summary>
        /// In theory this should output the same as OriginalQueryText
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var filter in IncludeFilters)
            {
                sb.Append(filter.GetQueryText());
                sb.Append(" ");
            }

            foreach (var filter in ExcludeFilters)
            {
                sb.Append("-");
                sb.Append(filter.GetQueryText());
                sb.Append(" ");
            }

            foreach (var filter in TagFilters)
            {
                sb.Append(filter.GetQueryText());
                sb.AppendFormat(":{0} ", Filter.WildcardChar);
            }

            string str = sb.ToString().Trim();
            return str;
        }


    }
}
