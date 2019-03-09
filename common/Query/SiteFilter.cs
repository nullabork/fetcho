using System;
using System.Linq;

namespace Fetcho.Common
{
    [Filter("site:[site|*][:site|*]")]
    public class SiteFilter : Filter
    {
        public string Site { get; set; }

        public override string Name => "Site filter";

        public SiteFilter(string site) : this()
            => Site = site;

        private SiteFilter() 
            => CallOncePerPage = true;

        public override string GetQueryText() 
            => string.Format("site:{0}", Site);

        public override string[] IsMatch(Uri uri, string fragment)
            => uri.Host.Contains(Site) ? new string[1] { uri.Host } : new string[0];

        /// <summary>
        /// Parse some text to create this object
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText)
        {
            string language = String.Empty;

            int index = queryText.IndexOf(':');
            if (index > -1)
            {
                language = queryText.Substring(index + 1);
                if (language == "*") language = String.Empty;
            }

            return new SiteFilter(language);
        }

        public static bool TokenIsFilter(string token) => token.StartsWith("site:");
    }

}
