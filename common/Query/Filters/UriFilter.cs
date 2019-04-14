using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("uri:", "uri:[uri_fragment|*][:uri_fragment|*]")]
    public class UriFilter : Filter
    {
        public string SearchText { get; set; }

        public override string Name => "URI filter";

        public override bool RequiresResultInput { get => true; }

        public override bool CallOncePerPage => true;

        public override bool IsReducingFilter => true;

        public UriFilter(string searchText)
            => SearchText = searchText;

        public override string GetQueryText()
            => string.Format("uri:{0}", SearchText);

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
            => result.Uri.Contains(SearchText) ? new string[1] { Utility.MakeTag(result.Uri) } : EmptySet;

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

            return new UriFilter(searchText);
        }
    }

}
