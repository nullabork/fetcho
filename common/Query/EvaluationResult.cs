namespace Fetcho.Common.QueryEngine
{
    public class EvaluationResult
    {
        public EvaluationResultAction Action { get; set; }

        public string[] Tags { get; set; }

        public EvaluationResult(EvaluationResultAction action, string[] tags)
        {
            Action = action;
            Tags = tags;
        }

    }
}
