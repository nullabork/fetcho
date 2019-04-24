using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter(
        "has:", 
        "has:property_name",
        Description = "Filter or tag by if the result has this property. eg. 'title', 'description'")]
    public class HasFilter : Filter
    {
        public string PropertyName { get; set; }

        public override string Name => "Has Property Filter";

        public override decimal Cost => 2m;

        public override bool RequiresResultInput { get => true; }

        public override bool IsReducingFilter => true;

        public override string GetQueryText()
            => string.Format("has:{0}", PropertyName);

        public HasFilter(string propertyName) => PropertyName = propertyName.ToLower();

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            if (result == null) return EmptySet;
            if (result.PropertyCache.ContainsKey(PropertyName))
            {
                var o = result.PropertyCache[PropertyName];
                if ( o == null || string.IsNullOrWhiteSpace(o.ToString())) return EmptySet;
                return new string[1] { Utility.MakeTag(o.ToString()) };
            }
            else
                return EmptySet; 
        }

        public static Filter Parse(string queryText, int depth)
        {
            string propertyName = string.Empty;

            var tokens = queryText.Split(':');
            if (tokens.Length != 2) return null;

            propertyName = tokens[1].Trim();

            return new HasFilter(propertyName);

        }
    }
}
