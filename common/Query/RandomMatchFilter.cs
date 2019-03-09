using System;

namespace Fetcho.Common
{
    [Filter("random:", "random:[probability][:*]")]
    public class RandomMatchFilter : Filter
    {
        public const double DefaultMatchProbability = MaxMatchProbability;
        public const double MaxMatchProbability = 1.0 / 10000.0;
        public const double MinMatchProbability = 1.0 / 10000000.0;

        private Uri lastUri = null;

        static readonly Random random = new Random(DateTime.Now.Millisecond);

        public double MatchProbability { get; set; }

        public override string Name => "RandomMatchFilter";

        public RandomMatchFilter(double probability)
        {
            CallOncePerPage = true;
            MatchProbability = probability.RangeConstraint(MinMatchProbability, MaxMatchProbability);
        }

        public RandomMatchFilter() => MatchProbability = DefaultMatchProbability;

        public override string[] IsMatch(Uri uri, string fragment)
        {
            bool rtn = lastUri != uri &&  random.NextDouble() < MatchProbability;
            lastUri = uri;

            if (rtn)
                return new string[1];
            else
                return new string[0];
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
