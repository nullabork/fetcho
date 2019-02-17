
using System;

namespace Fetcho.Common
{
    public interface IFilter
    {
        /// <summary>
        /// Name of this filter
        /// </summary>
        string Name { get;  }

        /// <summary>
        /// If the fragment is matched by this filter
        /// </summary>
        /// <param name="fragment"></param>
        /// <returns></returns>
        bool IsMatch(Uri uri, string fragment);

        /// <summary>
        /// Get the textual representation of this filter
        /// </summary>
        /// <returns></returns>
        string GetQueryText();
    }
}
