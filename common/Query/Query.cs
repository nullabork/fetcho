using System;
using System.Text;

namespace Fetcho.Common.QueryEngine
{
    public class Query
    {
        private FilterCollection IncludeFilters = new FilterCollection();
        private FilterCollection ExcludeFilters = new FilterCollection();
        private FilterCollection TagFilters = new FilterCollection();

        public Query(string queryText)
        {
            ParseQueryText(queryText);
            Console.WriteLine(this.ToString());
        }

        public EvaluationResult Evaluate(Uri uri, string words)
        {
            var action = EvaluationResultAction.NotEvaluated;

            var inc = IncludeFilters.AnyMatch(uri, words);
            var exc = ExcludeFilters.AnyMatch(uri, words);

            if (inc && !exc) action = EvaluationResultAction.Include;
            if (!inc && exc) action = EvaluationResultAction.Exclude;
            if (!inc && !exc) action = EvaluationResultAction.NotEvaluated;
            if (inc && exc) action = EvaluationResultAction.Include;

            TagFilters.AnyMatch(uri, words);
            var tags = new string[] { };

            return new EvaluationResult(action, tags);
        }

        private void ParseQueryText(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return;
            string[] tokens = text.Split(' ');

            foreach (var token in tokens)
            {
                if (!token.StartsWith("-", StringComparison.InvariantCultureIgnoreCase))
                    IncludeFilters.Add(new TextMatchFilter(token));
                else
                {
                    if (RandomMatchFilter.TokenIsFilter(token))
                        IncludeFilters.Add(RandomMatchFilter.Parse(token));
                    else
                        ExcludeFilters.Add(new TextMatchFilter(token));

                }
            }
        }

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

                sb.Append(filter.GetQueryText());
                sb.Append(" ");
            }

            foreach (var filter in TagFilters)
            {
                sb.Append(filter.GetQueryText());
                sb.Append(" ");
            }

            return sb.ToString().Trim();
        }

    }
}
