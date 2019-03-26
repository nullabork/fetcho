using System;
using MaxMind.GeoIP2.Responses;

namespace Fetcho.Common
{
    [Filter(
        "geo-ip-ll:",
        "geo-ip-ll:[[[x,y],[x,y]]|*][:[[x,y],[x,y]]|*]",
        Description = "Filter or tag by the Geo IP Coordinates")]
    public class GeoIPCoordinateFilter : GeoIPFilter
    {
        public BoundingBox Bounds { get; set; }

        public override string Name => "Geo IP Lat/Long";

        public override string Property => "ll";

        public override string FilterData => Bounds.ToString();

        public override decimal Cost => 1000m;

        public GeoIPCoordinateFilter(string llvector) => Bounds = BoundingBox.Parse(llvector);

        /// <summary>
        /// Parse some text into a TextMatchFilter
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText)
        {
            string vector = String.Empty;

            int index = queryText.IndexOf(':');
            if (index > -1)
            {
                vector = queryText.Substring(index + 1);
                if (vector == "*") vector = String.Empty;
            }

            return new GeoIPCoordinateFilter(vector);
        }

        protected override string[] GetTags(CityResponse cityResponse)
            => new string[] {
                    Utility.MakeTag(string.Format("[{0:0.000},{1:0.000}]", cityResponse.Location.Latitude, cityResponse.Location.Longitude))
                };

        public class BoundingBox
        {
            public double[] CoordOne { get; set; }
            public double[] CoordTwo { get; set; }

            public bool IsValid { get => CoordOne != null && CoordTwo != null && CoordOne.Length == 2 && CoordTwo.Length == 2; }

            public bool IsWithin(double latitude, double longitude)
                => IsValid &&
                latitude.IsBetween(Math.Min(CoordOne[0], CoordTwo[0]), Math.Max(CoordOne[0], CoordTwo[0])) &&
                longitude.IsBetween(Math.Min(CoordOne[1], CoordTwo[1]), Math.Max(CoordOne[1], CoordTwo[1]));

            public override string ToString()
                => string.Format("[[{0},{1}],[{2},{3}]]", CoordOne[0], CoordOne[1], CoordTwo[0], CoordTwo[1]);

            public static BoundingBox Parse(string llvector)
            {
                llvector = llvector.Trim();
                if (!llvector.StartsWith("[")) return null;
                if (!llvector.EndsWith("]")) return null;
                llvector = llvector.Substring(1, llvector.Length - 2);

                int idx = llvector.IndexOf(']');
                if (idx < 0) return null;
                var v1 = ParseSingleVector(llvector.Substring(1, idx - 2));

                idx = llvector.IndexOf('[', 2);
                if (idx < 0) return null;
                var v2 = ParseSingleVector(llvector.Substring(idx, llvector.Length - 2));

                return new BoundingBox
                {
                    CoordOne = v1,
                    CoordTwo = v2
                };
            }

            private static double[] ParseSingleVector(string vector)
            {
                string[] tokens = vector.Split(',');
                if (tokens.Length != 2) return null;
                if (!double.TryParse(tokens[0], out double lat) || !double.TryParse(tokens[1], out double longitude)) return null;
                return new double[] { lat, longitude };
            }
        }
    }

}
