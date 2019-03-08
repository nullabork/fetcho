using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public Query(string queryText)
        {
            ParseQueryText(queryText);
            Console.WriteLine(this.ToString());
        }

        public EvaluationResult Evaluate(Uri uri, string words)
        {
            var ticks = DateTime.Now.Ticks;
            var action = EvaluationResultAction.NotEvaluated;

            var inc = IncludeFilters.AnyMatch(uri, words);
            var exc = ExcludeFilters.AnyMatch(uri, words);

            if (inc && !exc) action = EvaluationResultAction.Include;
            if (!inc && exc) action = EvaluationResultAction.Exclude;
            if (!inc && !exc) action = EvaluationResultAction.NotEvaluated;
            if (inc && exc) action = EvaluationResultAction.Include;

            IEnumerable<string> tags = null;
            if ( action == EvaluationResultAction.Include)
              tags = TagFilters.GetTags(uri, words);

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
                string t = token;
                FilterMode filterMode = DetermineFilterMode(token);
                if (filterMode == FilterMode.Exclude) t = t.Substring(1); // chop off the '-'
                if (filterMode == FilterMode.Tag) t = t.Substring(0, t.LastIndexOf(':'));

                if (!IsComplexFilter(t))
                    AddFilter(new TextMatchFilter(t), filterMode);

                if (RandomMatchFilter.TokenIsFilter(token))
                    AddFilter(RandomMatchFilter.Parse(t), filterMode);

                else if (LanguageFilter.TokenIsFilter(t))
                    AddFilter(LanguageFilter.Parse(t), filterMode);

                else if (GeoIPCityFilter.TokenIsFilter(t))
                    AddFilter(GeoIPCityFilter.Parse(t), filterMode);
            }
        }

        private void AddFilter(Filter filter, FilterMode filterMode)
        {
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

                default:
                    Utility.LogInfo("Unknown filterMode {0}", filterMode);
                    break;
            }
        }

        private bool IsComplexFilter(string token)
            => token.Contains(":");

        private bool IsTagFilter(string token)
            => token.Split(':').Length == 3;

        private FilterMode DetermineFilterMode(string token)
            => token.StartsWith("-", StringComparison.InvariantCultureIgnoreCase) ? FilterMode.Exclude :
               IsTagFilter(token) ? FilterMode.Tag : FilterMode.Include;

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

    public enum FilterMode
    {
        Include, Exclude, Tag
    }
}
