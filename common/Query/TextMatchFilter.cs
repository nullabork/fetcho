using System;

namespace Fetcho.Common
{
    /// <summary>
    /// Simple text match filter to include results
    /// </summary>
    public class TextMatchFilter : IFilter
    {
        /// <summary>
        /// Text to match
        /// </summary>
        public string SearchText { get; set; }

        /// <summary>
        /// Name of this filter
        /// </summary>
        public string Name { get => "TextMatchFilter";  }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="searchText"></param>
        public TextMatchFilter(string searchText)
        {
            SearchText = searchText;
        }

        /// <summary>
        /// Can't create using the default constructor
        /// </summary>
        private TextMatchFilter() { }

        /// <summary>
        /// Try and match the fragment from the file
        /// </summary>
        /// <param name="fragment"></param>
        /// <returns></returns>
        public bool IsMatch(Uri uri, string fragment) => fragment.ToLower().Contains(SearchText.ToLower());

        /// <summary>
        /// Parse some text into a TextMatchFilter
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static IFilter Parse(string queryText) => new TextMatchFilter(queryText);

        /// <summary>
        /// Output as string
        /// </summary>
        /// <returns></returns>
        public string GetQueryText() => string.Format("{0}", SearchText);

        public static bool TokenIsFilter(string token) => true;
    }
}
