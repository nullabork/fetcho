using System;
using MaxMind.GeoIP2.Responses;

namespace Fetcho.Common
{
    [Filter(
        "geo-ip-subdivision:", 
        "geo-ip-subdivision:[subdivision|*][:subdivision|*]",
        Description = "Filter or tag by the Geo IP Sub Division")]
    public class GeoIPSubDivisionFilter : GeoIPFilter
    {
        public string SubDivision { get; set; }

        public override string Name => "Geo IP Sub Division";

        public override string Property => "subdivision";

        public override string FilterData => SubDivision;

        public override decimal Cost => 500m;

        public GeoIPSubDivisionFilter(string subDivision)
            => SubDivision = subDivision;


        /// <summary>
        /// Parse some text into a TextMatchFilter
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText)
        {
            string subDivision = String.Empty;

            int index = queryText.IndexOf(':');
            if (index > -1)
            {
                subDivision = queryText.Substring(index + 1);
                if (subDivision == "*") subDivision = String.Empty;
            }

            return new GeoIPSubDivisionFilter(subDivision);
        }

        protected override string[] GetTags(CityResponse cityResponse)
            => new string[] {
                    Utility.MakeTag(cityResponse.MostSpecificSubdivision.Name)
                };

    }

}
