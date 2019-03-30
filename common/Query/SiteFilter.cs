using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("site:", "site:[site|*][:site|*]")]
    public class SiteFilter : Filter
    {
        public string SearchText { get; set; }

        public override string Name => "Site filter";

        public SiteFilter(string site) : this()
            => SearchText = site;

        public override decimal Cost => 1m; 

        private SiteFilter() 
            => CallOncePerPage = true;

        public override string GetQueryText() 
            => string.Format("site:{0}", SearchText);

        public override string[] IsMatch(IWebResource resource, string fragment, Stream stream)
        {
            var uri = new Uri(resource.RequestProperties["uri"]);
            return uri.Host.Contains(SearchText) ? new string[1] { uri.Host } : EmptySet;
        }

        /// <summary>
        /// Parse some text to create this object
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText)
        {
            string searchText = String.Empty;

            int index = queryText.IndexOf(':');
            if (index > -1)
            {
                searchText = queryText.Substring(index + 1);
                if (searchText == WildcardChar) searchText = String.Empty;
            }

            return new SiteFilter(searchText);
        }
    }
}
