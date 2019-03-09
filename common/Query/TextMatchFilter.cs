using System;

namespace Fetcho.Common
{
    /// <summary>
    /// Simple text match filter to include results
    /// </summary>
    [Filter("NOWAYTOMATCHTHIS", "any_search_term")]
    public class TextMatchFilter : Filter
    {
        /// <summary>
        /// Text to match
        /// </summary>
        public string SearchText { get; set; }

        /// <summary>
        /// Name of this filter
        /// </summary>
        public override string Name { get => "TextMatchFilter";  }

        public override decimal Cost => 3m;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="searchText"></param>
        public TextMatchFilter(string searchText) 
            => SearchText = searchText;

        /// <summary>
        /// Can't create using the default constructor
        /// </summary>
        private TextMatchFilter() { }

        /// <summary>
        /// Try and match the fragment from the file
        /// </summary>
        /// <param name="fragment"></param>
        /// <returns></returns>
        public override string[] IsMatch(Uri uri, string fragment) 
            => fragment.ToLower().Contains(SearchText.ToLower()) ? new string[1] : new string[0];

        /// <summary>
        /// Output as string
        /// </summary>
        /// <returns></returns>
        public override string GetQueryText() 
            => string.Format("{0}", SearchText);

        /// <summary>
        /// Parse some text into a TextMatchFilter
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText)
            => new TextMatchFilter(queryText);

        public static bool TokenIsFilter(string token)
            => !token.Contains(":");
    }

}
