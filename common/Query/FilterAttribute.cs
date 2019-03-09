
using System;

namespace Fetcho.Common
{
    public class FilterAttribute : Attribute
    {
        public string ShortHelp { get; set; }

        public bool Hidden { get; set; }

        public FilterAttribute(string shortHelp)
            => ShortHelp = shortHelp;
    }
}
