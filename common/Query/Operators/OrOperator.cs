using System.IO;
using System.Linq;
using Fetcho.Common.Entities;

namespace Fetcho.Common.QueryEngine.Operators
{
    public class OrOperator : Filter
    {
        public Filter Left { get; set; }
        public Filter Right { get; set; }

        public override string Name => "OR";

        public override decimal Cost => Left.Cost + Right.Cost;

        public override bool IsReducingFilter => Left.IsReducingFilter || Right.IsReducingFilter;

        public override bool RequiresResultInput => Left.RequiresResultInput || Right.RequiresResultInput;
        public override bool RequiresTextInput => Left.RequiresTextInput || Right.RequiresTextInput;
        public override bool RequiresStreamInput => Left.RequiresStreamInput || Right.RequiresStreamInput;

        public override bool CallOncePerPage => Left.CallOncePerPage || Right.CallOncePerPage;

        public OrOperator(Filter left, Filter right)
        {
            ThrowIfIsNull(left, right);

            Left = left;
            Right = right;
        }

        public override string GetQueryText()
            => string.Format("{0} OR {1}", Left.GetQueryText(), Right.GetQueryText()); 

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            var tags = Left.IsMatch(result, fragment, stream);
            if (tags.Any()) return tags;
            else tags = Right.IsMatch(result, fragment, stream);
            return tags;
        }

        private void ThrowIfIsNull(Filter left, Filter right)
        {
            if (left == null)
                throw new InvalidObjectFetchoException("Left is null");
            if (right == null)
                throw new InvalidObjectFetchoException("Right is null");
        }
    }
}
