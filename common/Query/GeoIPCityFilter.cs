using System;
using MaxMind.GeoIP2.Responses;

namespace Fetcho.Common
{
    [Filter("geo-ip-city:[city|*][:city|*]")]
    public class GeoIPCityFilter : GeoIPFilter
    {
        public string City { get; set; }

        public override string Name => "Geo IP City";

        public override string Property => "city";

        public override string FilterData => City;

        public override decimal Cost => 1000m;

        public GeoIPCityFilter(string city) => City = city;

        /// <summary>
        /// Parse some text into a TextMatchFilter
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText)
        {
            string city = String.Empty;

            int index = queryText.IndexOf(':');
            if (index > -1)
            {
                city = queryText.Substring(index + 1);
                if (city == "*") city = String.Empty;
            }

            return new GeoIPCityFilter(city);
        }

        protected override string[] GetTags(CityResponse cityResponse)
            => new string[] {
                    Utility.MakeTag(cityResponse.City.Name),
                    Utility.MakeTag(cityResponse.Country.Name)
                };

        public static bool TokenIsFilter(string token) => token.StartsWith("geo-ip-city:");
    }

}
