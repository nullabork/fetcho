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
        Description = "Filter by a tag")]
    public class TagFilter : Filter
    {
        public string TagName { get; set; }

        public override string Name => "Tag Filter";

        // needs to run after all other filters
        public override decimal Cost => MaxCost;

        public override bool CallOncePerPage => true;

        public override bool RequiresResultInput { get => true; }

        public override bool IsReducingFilter => true;

        public override string GetQueryText()
            => string.Format("tag:{0}", TagName);

        public TagFilter(string tagName) 
            => TagName = tagName.ToLower();

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            foreach (var tag in result.Tags)
                if (tag.IndexOf(TagName, StringComparison.InvariantCultureIgnoreCase) > -1)
                    return new string[] { Utility.MakeTag(TagName) };
            return EmptySet;
        }

        public static Filter Parse(string queryText, int depth)
        {
            string searchText = string.Empty;

            var tokens = queryText.Split(':');
            if (tokens.Length != 2) return null;

            searchText = tokens[1].Trim();

            return new TagFilter(searchText);

        }
    }
}
