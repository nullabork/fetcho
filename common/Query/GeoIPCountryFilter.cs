﻿using System;
using MaxMind.GeoIP2.Responses;

namespace Fetcho.Common
{
    [Filter("geo-ip-country:[country|*][:country|*]")]
    public class GeoIPCountryFilter : GeoIPFilter
    {
        public string Country { get; set; }

        public override string Name => "Geo IP Country";

        public override string Property => "country";

        public override string FilterData => Country;

        public override decimal Cost => 1000m;

        public GeoIPCountryFilter(string country) 
            => Country = country;


        /// <summary>
        /// Parse some text into a TextMatchFilter
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText)
        {
            string country = String.Empty;

            int index = queryText.IndexOf(':');
            if (index > -1)
            {
                country = queryText.Substring(index + 1);
                if (country == "*") country = String.Empty;
            }

            return new GeoIPCountryFilter(country);
        }

        protected override string[] GetTags(CityResponse cityResponse)
            => new string[] {
                    Utility.MakeTag(cityResponse.Country.Name)
                };

        public static bool TokenIsFilter(string token) => token.StartsWith("geo-ip-country:");
    }

}
