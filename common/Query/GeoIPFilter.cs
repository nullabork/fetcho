using System;
using System.Linq;
using System.Net;
using Fetcho.Common.Entities;
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


        public override string[] IsMatch(IWebResource resource, string fragment)
        {
            const string HostIPCacheKey = "hostip";

            try
            {
                var uri = new Uri(resource.RequestProperties["uri"]);

                if ( !resource.PropertyCache.ContainsKey(HostIPCacheKey))
                    resource.PropertyCache.Add(HostIPCacheKey, Utility.GetHostIPAddress(uri).GetAwaiter().GetResult());

                IPAddress ip = resource.PropertyCache[HostIPCacheKey] as IPAddress;

                var c = database.City(ip);

                return GetTags(c).Where(x => String.IsNullOrWhiteSpace(FilterData) || x.Contains(FilterData)).ToArray();
            }
            catch (AddressNotFoundException ex)
            {
                return new string[] { };
            }
        }

        protected abstract string[] GetTags(CityResponse cityResponse);

    }

}
