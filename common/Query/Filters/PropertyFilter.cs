using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("property(", "property(propertyName):[value match|*][:value match|*]",
        Description = "Filter or tag by a specific property key")]
    public class PropertyFilter : Filter
    {
        const string PropertyFilterKey = "property(";

        public string SearchText { get; set; }

        public string HeaderKey { get; set; }

        public override string Name => "Property Filter";

        public override bool CallOncePerPage => true;

        public override decimal Cost => 50m;

        public override bool RequiresResultInput { get => true; }
        public override bool RequiresStreamInput { get => true; } // indirectly via properties

        public override bool IsReducingFilter => true;

        public PropertyFilter(string headerKey, string searchText)
        {
            SearchText = searchText.ToLower();
            HeaderKey = headerKey.ToLower();
        }

        public override string GetQueryText()
            => string.Format("{0}{1}):{2}", 
                PropertyFilterKey, 
                HeaderKey,
                string.IsNullOrWhiteSpace(SearchText) ? WildcardChar : SearchText
                );

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            object o = null;
            if (result.PropertyCache.ContainsKey(HeaderKey))
                o = result.PropertyCache[HeaderKey];

            if (o == null) return EmptySet;

            if (string.IsNullOrWhiteSpace(SearchText) || o.ToString().Contains(SearchText))
                return new string[1] { Utility.MakeTag(o?.ToString()) };
            else
                return EmptySet;
        }

        /// <summary>
        /// Parse some text to create this object
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText, int depth)
        {
            string searchText = string.Empty;

            var tokens = queryText.Split(':');
            if (tokens.Length != 2) return null;

            var key = tokens[0].Substring(PropertyFilterKey.Length, tokens[0].Length - PropertyFilterKey.Length - 1);
            searchText = tokens[1].Trim().Replace(WildcardChar,"");

            return new PropertyFilter(key, searchText);
        }
    }

}
