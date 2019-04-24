using System;
using MaxMind.GeoIP2.Responses;

namespace Fetcho.Common
{
    [Filter(
        "geo-ip-city:", 
        "geo-ip-city:[city|*][:city|*]", 
        Description = "Filter or tag by the Geo IP City"
        )]
    public class GeoIPCityFilter : GeoIPFilter
    {
        public string City { get; set; }

        public override string Name => "Geo IP City";

        public override string Property => "city";

        public override string FilterData => City;

        public override decimal Cost => 500m;

        public GeoIPCityFilter(string city) => City = city;

        /// <summary>
        /// Parse some text into a TextMatchFilter
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText, int depth)
        {
            string city = string.Empty;

            int index = queryText.IndexOf(':');
            if (index > -1)
            {
                city = queryText.Substring(index + 1);
                if (city == "*") city = string.Empty;
            }

            return new GeoIPCityFilter(city);
        }

        protected override string[] GetTags(CityResponse cityResponse)
            => new string[] {
                    Utility.MakeTag(cityResponse.City.Name),
                    Utility.MakeTag(cityResponse.Country.Name)
                };
    }

}
