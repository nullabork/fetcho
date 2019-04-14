using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Fetcho.Common.Entities;

namespace Fetcho.Common.QueryEngine
{
    /// <summary>
    /// A collection of filters and how they operate together
    /// </summary>
    public class Query
    {
        public FilterCollection IncludeFilters { get; }
        public FilterCollection ExcludeFilters { get; }
        public FilterCollection TagFilters { get; }

        public long MinCost { get; set; }
        public long MaxCost { get; set; }
        public long AvgCost { get => NumberOfEvaluations == 0 ? -1 : TotalCost / NumberOfEvaluations; }
        public long TotalCost { get; set; }
        public int NumberOfEvaluations { get; set; }
        public int NumberOfInclusions { get; set; }
        public int NumberOfExclusions { get; set; }
        public int NumberOfTags { get; set; }
        public string OriginalQueryText { get; set; }

        public bool RequiresTextInput { get; protected set; }
        public bool RequiresStreamInput { get; protected set; }
        public bool RequiresResultInput { get; protected set; }

        public Query(string queryText, int depth = 0)
        {
            OriginalQueryText = queryText;
            MinCost = long.MaxValue;
            MaxCost = -1;
            TotalCost = -1;
            IncludeFilters = new FilterCollection();
            ExcludeFilters = new FilterCollection();
            TagFilters = new FilterCollection();
            RequiresResultInput = false;
            RequiresStreamInput = false;
            RequiresTextInput = false;
            ParseQueryText(queryText, depth);
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

            var inc = IncludeFilters.AllMatch(result, words, stream);

            if (inc)
            {
                var exc = ExcludeFilters.AnyMatch(result, words, stream);

                if (exc)
                    action = EvaluationResultAction.Exclude;
                else
                {
                    action = EvaluationResultAction.Include;
                    tags = TagFilters.GetTags(result, words, stream);
                }
            }

            var r = new EvaluationResult(action, tags, DateTime.UtcNow.Ticks - ticks);
            DoBookKeeping(r);
            return r;
        }

        /// <summary>
        /// Distill the results by this query
        /// </summary>
        /// <param name="results"></param>
        /// <returns></returns>
        public IEnumerable<WorkspaceResult> Distill(IEnumerable<WorkspaceResult> results)
            => results.Where((result) => Evaluate(result, String.Empty, null).Action == EvaluationResultAction.Include);

        /// <summary>
        /// Parse query text to build the filter collections
        /// </summary>
        /// <param name="text">The query text</param>
        /// <param name="depth">Depth of this query to throw exceptions if we have too much sub query depth</param>
        private void ParseQueryText(string text, int depth)
        {
            if (String.IsNullOrWhiteSpace(text)) return;

            var tokens = TokeniseQueryText(text);

            foreach (var token in tokens)
            {
                string filterToken = token;
                string tagToken = String.Empty;
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
                    filter = new SimpleTextMatchFilter(filterToken);
                else
                {
                    filter = Filter.CreateFilter(filterToken, depth);
                    tagger = Filter.CreateFilter(tagToken, depth);
                }

                CalculateFilteringDataRequirements(filter);
                CalculateFilteringDataRequirements(tagger);

                switch (filterMode)
                {
                    case FilterMode.Include:
                        if (filter == null || !filter.IsReducingFilter) break;
                        IncludeFilters.Add(filter);
                        break;

                    case FilterMode.Exclude:
                        if (filter == null || !filter.IsReducingFilter) break;
                        ExcludeFilters.Add(filter);
                        break;

                    case FilterMode.Tag:
                        if (filter == null) break;
                        TagFilters.Add(filter);
                        break;

                    case (FilterMode.Tag | FilterMode.Include):
                        if (filter != null && filter.IsReducingFilter) IncludeFilters.Add(filter);
                        if (tagger != null) TagFilters.Add(tagger);
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
            if (r.Action == EvaluationResultAction.Exclude) NumberOfExclusions++;
            else if (r.Action == EvaluationResultAction.Include) NumberOfInclusions++;
            else if (r.Action == EvaluationResultAction.Tag) NumberOfTags++;
        }

        /// <summary>
        /// Builds a string of the cost details of this query
        /// </summary>
        /// <returns></returns>
        public string CostDetails()
            => String.Format("Min: {0,-10}, Max: {1, -10}, Avg: {2,-10}, Total: {3,-10}, #: {4,-10}",
                MinCost, MaxCost, AvgCost, TotalCost, NumberOfEvaluations);

        /// <summary>
        /// Removes comment and new line chars etc.
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static string StripCommentsAndLines(string queryText)
        {
            var sb = new StringBuilder();

            using (var sr = new StringReader(queryText))
            {
                while (sr.Peek() > -1)
                {
                    var line = sr.ReadLine().Trim();

                    int commentIdx = line.IndexOf("//");
                    if (commentIdx > -1)
                    {
                        line = line.Substring(0, commentIdx).Trim();
                    }

                    sb.Append(line.Replace("\t", " "));
                    sb.Append(" ");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Extracts each query command from the query text
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static IEnumerable<string> TokeniseQueryText(string queryText)
        {
            var l = new List<string>();
            queryText = StripCommentsAndLines(queryText);

            bool inString = false;
            var sb = new StringBuilder();
            for (int i = 0; i < queryText.Length; i++)
            {
                if (Char.IsWhiteSpace(queryText[i]) && !inString)
                {
                    l.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    if (queryText[i] == '"' && (i == 0 || queryText[i - 1] != '\\'))
                        inString = !inString;
                    else
                        sb.Append(queryText[i]);
                }
            }

            l.Add(sb.ToString());

            return l.Where(x => !String.IsNullOrWhiteSpace(x)).Distinct();
        }

        /// <summary>
        /// Updates the requirements of this query by the passed filter and tagger
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="tagger"></param>
        private void CalculateFilteringDataRequirements(Filter filter)
        {
            RequiresResultInput = RequiresResultInput || (filter != null && filter.RequiresResultInput);
            RequiresTextInput = RequiresTextInput || (filter != null && filter.RequiresTextInput);
            RequiresStreamInput = RequiresStreamInput || (filter != null && filter.RequiresStreamInput);
        }

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
