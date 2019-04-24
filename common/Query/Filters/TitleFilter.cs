using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("title:", "title:[text|*][:text|*]")]
    public class TitleFilter : Filter
    {
        public string SearchText { get; set; }

        public override string Name => "Title filter";

        public TitleFilter(string title)
            => SearchText = title;

        public override decimal Cost => 1m;

        public override bool CallOncePerPage => true;

        public override bool IsReducingFilter => !string.IsNullOrWhiteSpace(SearchText);

        public override bool RequiresResultInput { get => true; }

        public override string GetQueryText()
            => string.Format("title:{0}", SearchText);

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            return result.Title.Contains(SearchText) ? new string[1] : EmptySet;
        }

        /// <summary>
        /// Parse some text to create this object
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText, int depth)
        {
            string searchText = string.Empty;

            int index = queryText.IndexOf(':');
            if (index > -1)
            {
                searchText = queryText.Substring(index + 1);
                if (searchText == WildcardChar) searchText = string.Empty;
            }

            return new TitleFilter(searchText);
        }
    }
}
