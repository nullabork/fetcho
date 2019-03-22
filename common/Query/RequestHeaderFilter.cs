using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("request-header(", "request-header(key name):[value match|*][:value match|*]",
        Description = "Filter or tag by a specific request header key")]
    public class RequestHeaderFilter : Filter
    {
        const string RequestHeaderFilterKey = "request-header(";

        public string SearchText { get; set; }

        public string HeaderKey { get; set; }

        public override string Name => "Request Header Filter";

        public RequestHeaderFilter(string headerKey, string searchText) : this()
        {
            SearchText = searchText.ToLower();
            HeaderKey = headerKey.ToLower();
        }

        private RequestHeaderFilter()
            => CallOncePerPage = true;

        public override string GetQueryText()
            => string.Format("{0}{1}):{2}", RequestHeaderFilterKey, HeaderKey, SearchText);

        public override string[] IsMatch(IWebResource resource, string fragment, Stream stream)
            => resource.RequestProperties.ContainsKey(HeaderKey) 
               && (String.IsNullOrWhiteSpace(SearchText) || resource.RequestProperties[HeaderKey].Contains(SearchText)) ? 
                    new string[1] { Utility.MakeTag(resource.RequestProperties[HeaderKey]) } : EmptySet;

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

            var key = tokens[0].Substring(RequestHeaderFilterKey.Length, tokens[0].Length - RequestHeaderFilterKey.Length - 1);
            searchText = tokens[1].Trim();

            return new RequestHeaderFilter(key, searchText);
        }
    }

}
