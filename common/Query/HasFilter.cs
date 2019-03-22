using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter(
        "has:", 
        "has:[property_name|*][:property_name|*]",
        Description = "Filter or tag by if the result has this property. eg. 'title', 'description'")]
    public class HasFilter : Filter
    {
        public string PropertyName { get; set; }

        public override string Name => "Has Property Filter";

        public override decimal Cost => 2m;

        public override string GetQueryText()
            => string.Format("has:{0}", PropertyName);

        public HasFilter(string propertyName) => PropertyName = propertyName.ToLower();

        public override string[] IsMatch(IWebResource resource, string fragment, Stream stream)
        {
            if (resource.PropertyCache.ContainsKey(PropertyName))
            {
                var o = resource.PropertyCache[PropertyName];
                if ( o == null || String.IsNullOrWhiteSpace(o.ToString())) return EmptySet;
                return new string[1] { "has-" + PropertyName };
            }
            else
                return EmptySet; 
        }

        public static Filter Parse(string queryText)
        {
            string searchText = String.Empty;

            var tokens = queryText.Split(':');
            if (tokens.Length != 2) return null;

            searchText = tokens[1].Trim();

            return new HasFilter(searchText);

        }
    }

}
