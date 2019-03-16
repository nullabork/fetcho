using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Fetcho.Common.Entities;

namespace Fetcho.Common.QueryEngine
{
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

        public Query(string queryText)
        {
            OriginalQueryText = queryText;  
            ParseQueryText(queryText);
            Console.WriteLine(this.ToString());
        }

        public EvaluationResult Evaluate(IWebResource resource, string words, Stream stream)
        {
            var ticks = DateTime.Now.Ticks;
            var action = EvaluationResultAction.NotEvaluated;

            var inc = IncludeFilters.AllMatch(resource, words, stream);
            var exc = ExcludeFilters.AnyMatch(resource, words, stream);

            if (inc && !exc) action = EvaluationResultAction.Include;
            if (!inc && exc) action = EvaluationResultAction.Exclude;
            if (!inc && !exc) action = EvaluationResultAction.NotEvaluated;
            if (inc && exc) action = EvaluationResultAction.Include;

            IEnumerable<string> tags = null;
            if (action == EvaluationResultAction.Include)
                tags = TagFilters.GetTags(resource, words, stream);

            var r = new EvaluationResult(action, tags, DateTime.Now.Ticks - ticks);
            DoBookKeeping(r);
            return r;
        }

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


        private bool IsComplexFilter(string token)
            => token.Contains(":");

        private bool IsWildcardSearch(string token)
            => token.EndsWith("*");

        private bool IsTagFilter(string token)
            => token.Split(':').Length == 3;

        private bool IsDiscriminatingFilter(string token)
        {
            var ts = token.Split(':');
            if (ts.Length < 2) return true;
            if (ts[1].Trim().Length == 0) return false;
            if (ts[1].Trim().Length == 1 && ts[1].Trim() == "*") return false;
            return true;
        }

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

        private void DoBookKeeping(EvaluationResult r)
        {
            if (r.Cost < MinCost) MinCost = r.Cost;
            if (r.Cost > MaxCost) MaxCost = r.Cost;
            TotalCost += r.Cost;
            NumberOfEvaluations++;
        }

        public string CostDetails()
            => String.Format("Min: {0}, Max: {1}, Avg: {2}, Total: {3}, #: {4}", MinCost, MaxCost, AvgCost, TotalCost, NumberOfEvaluations);


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
                sb.Append(":* ");
            }

            return sb.ToString().Trim();
        }

    }
}
