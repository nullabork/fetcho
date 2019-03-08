using System;
using MaxMind.GeoIP2.Exceptions;
using MaxMind.GeoIP2.Responses;

namespace Fetcho.Common
{
    public class GeoIPCityFilter : Filter
    {
        public static MaxMind.GeoIP2.DatabaseReader database =
            new MaxMind.GeoIP2.DatabaseReader(FetchoConfiguration.Current.GeoIP2CityDatabasePath);

        public string City { get; set; }

        public override string Name => "Geo IP City";

        public GeoIPCityFilter(string city) => City = city;

        public override string GetQueryText() => string.Format("geo-ip-city:{0}", City);

        public override string[] IsMatch(Uri uri, string fragment)
        {
            try
            {
                var c = database.City(Utility.GetHostIPAddress(uri).GetAwaiter().GetResult());

                return GetTags(c);
            }
            catch (AddressNotFoundException ex)
            {
                return new string[] { };
            }
        }

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

        public static bool TokenIsFilter(string token) => token.StartsWith("geo-ip-city:");

        private string[] GetTags(CityResponse cityResponse)
        {
            return new string[] {
                    Utility.MakeTag(cityResponse.City.Name),
                    Utility.MakeTag(cityResponse.Country.Name)
                };
        }
    }

}
