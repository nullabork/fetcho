
using System;

namespace Fetcho.Common
{
    public abstract class Filter
    {
        /// <summary>
        /// Name of this filter
        /// </summary>
        public abstract string Name { get;  }

        public decimal Cost { get => 1m;  }

        /// <summary>
        /// If the fragment is matched by this filter
        /// </summary>
        /// <param name="fragment"></param>
        /// <returns></returns>
        public abstract string[] IsMatch(Uri uri, string fragment);

        /// <summary>
        /// Get the textual representation of this filter
        /// </summary>
        /// <returns></returns>
        public abstract string GetQueryText();
    }
}
