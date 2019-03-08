using System.Collections.Generic;

namespace Fetcho.Common.QueryEngine
{
    public class EvaluationResult
    {
        public EvaluationResultAction Action { get; set; }

        public IEnumerable<string> Tags { get; set; }

        public long Cost { get; set; }

        public EvaluationResult(EvaluationResultAction action, IEnumerable<string> tags, long cost)
        {
            Action = action;
            Tags = tags;
            Cost = cost;
        }

    }
}
