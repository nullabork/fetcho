
using System;
using System.Collections.Generic;
using System.Linq;

namespace Fetcho.Common
{
    public abstract class Filter
    {
        /// <summary>
        /// Name of this filter
        /// </summary>
        public abstract string Name { get;  }

        public virtual decimal Cost { get => 1m;  } // relative to the cheapest filter (Site)

        public bool CallOncePerPage { get; set; }

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

        public static IEnumerable<Type> GetAllFilterTypes()
            => typeof(Filter).Assembly.GetTypes().Where(x => x.IsSubclassOf(typeof(Filter)));
    }
}
