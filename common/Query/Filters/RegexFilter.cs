using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("regex:", "regex:[pattern|*][:tag_pattern|*]")]
    public class RegexFilter : Filter
    {
        private FastLookupCache<string> seenFragments = new FastLookupCache<string>(1000);
        private Regex matcher = null;

        public string RegexPattern { get; set; }

        public override string Name 
            => "Site filter";

        public RegexFilter(string regexPattern)
        {
            RegexPattern = regexPattern;
            matcher = new Regex(RegexPattern);
        }

        public override decimal Cost 
            => 50m;

        public override bool RequiresTextInput { get => true; }

        public override bool IsReducingFilter 
            => !String.IsNullOrWhiteSpace(RegexPattern);

        public override string GetQueryText()
            => string.Format("regex:{0}", RegexPattern);

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            var match = matcher.Match(fragment);
            if (match.Success)
            {
                int idx = match.Index;
                var frag = fragment.Fragment(idx, 40, 40);
                if (seenFragments.Contains(frag))
                {
                    // we've seen this fragment recently, nerf it even if it matches
                    // should get rid of menu links referring to the same link over and over
                    seenFragments.Enqueue(frag);
                    return EmptySet;
                }
                else
                {
                    // we haven't seen this yet
                    seenFragments.Enqueue(frag);

                    return Utility.MakeTags(match.Groups.OfType<object>().Select( x => x.ToString())).Distinct().ToArray();
                }
            }
            else
            {
                // no matches
                return EmptySet;
            }
        }

        /// <summary>
        /// Parse some text to create this object
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText, int depth)
        {
            string regexPattern = String.Empty;

            int index = queryText.IndexOf(':');
            if (index > -1)
            {
                regexPattern = queryText.Substring(index + 1);
            }

            return new RegexFilter(regexPattern);
        }
    }
}
