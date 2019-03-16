using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("random:", "random:[probability][:*]")]
    public class RandomMatchFilter : Filter
    {
        public const double DefaultMatchProbability = MaxMatchProbability;
        public const double MaxMatchProbability = 1.0 / 10000.0;
        public const double MinMatchProbability = 1.0 / 10000000.0;

        static readonly Random random = new Random(DateTime.Now.Millisecond);

        public double MatchProbability { get; set; }

        public override string Name => "RandomMatchFilter";

        public RandomMatchFilter(double probability)
        {
            CallOncePerPage = true;
            MatchProbability = probability.RangeConstraint(MinMatchProbability, MaxMatchProbability);
        }

        public RandomMatchFilter() => MatchProbability = DefaultMatchProbability;

        public override string[] IsMatch(IWebResource resource, string fragment, Stream stream)
        {
            bool rtn = random.NextDouble() < MatchProbability;

            if (rtn)
                return new string[1];
            else
                return EmptySet;
        }

        public override string GetQueryText()
            => string.Format("random:{0}", MatchProbability);

        public static RandomMatchFilter Parse(string token)
        {
            double prob = DefaultMatchProbability;

            int index = token.IndexOf(":");
            if ( index > -1 )
            {
                if (!double.TryParse(token.Substring(index + 1), out prob))
                    prob = DefaultMatchProbability;
            }

            return new RandomMatchFilter(prob);
        }
    }
}
