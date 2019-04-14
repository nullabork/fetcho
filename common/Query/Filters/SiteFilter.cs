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

        public SiteFilter(string site) 
            => SearchText = site;

        public override decimal Cost => 1m;

        public override bool CallOncePerPage => true;

        public override bool IsReducingFilter => !String.IsNullOrWhiteSpace(SearchText); 

        public override bool RequiresResultInput { get => true; }

        public override string GetQueryText()
            => string.Format("site:{0}", SearchText);

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            var uri = new Uri(result.Uri);
            return uri.Host.Contains(SearchText) ? new string[1] { Utility.MakeTag(uri.Host) } : EmptySet;
        }

        /// <summary>
        /// Parse some text to create this object
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText, int depth)
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
