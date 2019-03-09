using System;
using MaxMind.GeoIP2.Exceptions;
using MaxMind.GeoIP2.Responses;

namespace Fetcho.Common
{
    public abstract class GeoIPFilter : Filter
    {
        protected static MaxMind.GeoIP2.DatabaseReader database =
            new MaxMind.GeoIP2.DatabaseReader(FetchoConfiguration.Current.GeoIP2CityDatabasePath);

        public abstract string Property { get; }

        public abstract string FilterData { get; }

        public override string GetQueryText()
            => string.Format("geo-ip-{0}:{1}", Property, FilterData);


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

        protected abstract string[] GetTags(CityResponse cityResponse);

    }

}
