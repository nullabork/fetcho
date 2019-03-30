using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    /// <summary>
    /// Use this to filter out results by a tag
    /// </summary>
    [Filter(
        "tag:",
        "tag:[tagfilter|*][:replace_with]",
        Description = "Filter by a tag",
        Hidden = true)]
    public class TagFilter : Filter
    {
        public string TagName { get; set; }

        public override string Name => "Tag Filter";

        public override decimal Cost => 999999m;

        public override string GetQueryText()
            => string.Format("tag:{0}", TagName);

        public TagFilter(string tagName) => TagName = tagName.ToLower();

        public override string[] IsMatch(IWebResource resource, string fragment, Stream stream)
        {
            return EmptySet;
        }

        public static Filter Parse(string queryText)
        {
            string searchText = String.Empty;

            var tokens = queryText.Split(':');
            if (tokens.Length != 2) return null;

            searchText = tokens[1].Trim();

            return new TagFilter(searchText);

        }
    }
}
