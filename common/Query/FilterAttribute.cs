
using System;

namespace Fetcho.Common
{
    public class FilterAttribute : Attribute
    {
        public string TokenMatch { get; set; }

        public string ShortHelp { get; set; }

        public bool Hidden { get; set; }
        
        public FilterAttribute(string tokenMatch)
        {
            TokenMatch = tokenMatch;
        }

        public FilterAttribute(string tokenMatch, string shortHelp)
        {
            TokenMatch = tokenMatch;
            ShortHelp = shortHelp;
        }
    }
}
