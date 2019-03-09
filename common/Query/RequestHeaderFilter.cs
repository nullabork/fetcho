using System;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("request-header-", "request-header-[key name]:[value match|*][:value match|*]")]
    public class RequestHeaderFilter : Filter
    {
        const string RequestHeaderFilterKey = "request-header-";

        public string SearchText { get; set; }

        public string HeaderKey { get; set; }

        public override string Name => "Request Header Filter";

        public RequestHeaderFilter(string headerKey, string searchText) : this()
        {
            SearchText = searchText;
            HeaderKey = headerKey;
        }

        private RequestHeaderFilter()
            => CallOncePerPage = true;

        public override string GetQueryText()
            => string.Format("{0}{1}:{2}", RequestHeaderFilterKey, HeaderKey, SearchText);

        public override string[] IsMatch(IWebResource resource, string fragment)
            => resource.RequestProperties.ContainsKey(HeaderKey) 
               && resource.RequestProperties[HeaderKey].Contains(SearchText) ? 
                    new string[1] { Utility.MakeTag(resource.RequestProperties[HeaderKey]) } : new string[0];

        /// <summary>
        /// Parse some text to create this object
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText)
        {
            string searchText = String.Empty;

            var tokens = queryText.Split(':');
            if (tokens.Length != 2) return null;

            var key = tokens[0].Substring(RequestHeaderFilterKey.Length);
            searchText = tokens[1].Trim();

            return new RequestHeaderFilter(key, searchText);
        }
    }

}
