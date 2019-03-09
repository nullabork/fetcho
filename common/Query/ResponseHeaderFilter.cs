using System;
using System.Linq;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("response-header-", "response-header-[key name]:[value match|*][:value match|*]")]
    public class ResponseHeaderFilter : Filter
    {
        const string ResponseHeaderFilterKey = "response-header-";

        public string SearchText { get; set; }

        public string HeaderKey { get; set; }

        public override string Name => "Response Header Filter";

        public ResponseHeaderFilter(string headerKey, string searchText) : this()
        {
            SearchText = searchText;
            HeaderKey = headerKey;
        }

        private ResponseHeaderFilter()
            => CallOncePerPage = true;

        public override string GetQueryText()
            => string.Format("{0}{1}:{2}", ResponseHeaderFilterKey, HeaderKey, SearchText);

        public override string[] IsMatch(IWebResource resource, string fragment)
            => resource.ResponseProperties.ContainsKey(HeaderKey)
               && resource.ResponseProperties[HeaderKey].Contains(SearchText) ?
                      new string[1] { Utility.MakeTag(resource.ResponseProperties[HeaderKey]) } : new string[0];

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

            var key = tokens[0].Substring(ResponseHeaderFilterKey.Length);
            searchText = tokens[1].Trim();

            return new ResponseHeaderFilter(key, searchText);
        }
    }

}
