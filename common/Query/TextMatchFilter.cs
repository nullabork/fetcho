namespace Fetcho.Common
{
    /// <summary>
    /// Simple text match filter to include results
    /// </summary>
    public class TextMatchFilter : IFilter
    {
        /// <summary>
        /// What to do when matching?
        /// </summary>
        public MatchAction MatchAction { get; set; }

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
        /// <param name="matchAction"></param>
        public TextMatchFilter(string searchText, MatchAction matchAction)
        {
            SearchText = searchText;
            MatchAction = matchAction;
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
        public bool IsMatch(string fragment) => fragment.ToLower().Contains(SearchText.ToLower());

        /// <summary>
        /// Parse some text into a TextMatchFilter
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static IFilter Parse(string queryText) => new TextMatchFilter(queryText, MatchAction.Include);

        /// <summary>
        /// Output as string
        /// </summary>
        /// <returns></returns>
        public override string ToString() => string.Format("{0}", SearchText);

        /// <summary>
        /// Gets the tag to apply
        /// </summary>
        /// <returns></returns>
        public string GetTagName() => Name;
    }
}
