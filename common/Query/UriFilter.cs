using System;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("uri:", "uri:[uri_fragment|*][:uri_fragment|*]")]
    public class UriFilter : Filter
    {
        public string SearchText { get; set; }

        public override string Name => "URI filter";

        public UriFilter(string searchText) : this()
            => SearchText = searchText;

        private UriFilter()
            => CallOncePerPage = true;

        public override string GetQueryText()
            => string.Format("uri:{0}", SearchText);

        public override string[] IsMatch(IWebResource resource, string fragment)
            => resource.RequestProperties["uri"].Contains(SearchText) ? new string[1] { resource.RequestProperties["uri"] } : new string[0];

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
                if (searchText == "*") searchText = String.Empty;
            }

            return new UriFilter(searchText);
        }
    }

}
