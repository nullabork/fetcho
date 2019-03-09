
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    public abstract class Filter
    {
        /// <summary>
        /// Name of this filter
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// A cost factor relative to the faster filter
        /// </summary>
        public virtual decimal Cost { get => 1m; } // relative to the cheapest filter (Site)

        /// <summary>
        /// An optimisation to stop this filter being used more than one per page
        /// </summary>
        public bool CallOncePerPage { get; set; }

        /// <summary>
        /// If the fragment is matched by this filter
        /// </summary>
        /// <param name="fragment"></param>
        /// <returns></returns>
        public abstract string[] IsMatch(IWebResource resource, string fragment);

        /// <summary>
        /// Get the textual representation of this filter
        /// </summary>
        /// <returns></returns>
        public abstract string GetQueryText();

        /// <summary>
        /// Gets all filter types declared in the code base
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Type> GetAllFilterTypes()
            => typeof(Filter).Assembly.GetTypes().Where(x => x.IsSubclassOf(typeof(Filter)));

        /// <summary>
        /// Cache of all the filter types
        /// </summary>
        public static Dictionary<string, Type> FilterTypes = new Dictionary<string, Type>();

        /// <summary>
        /// Create the cache of filter types
        /// </summary>
        public static void InitaliseFilterTypes()
        {
            foreach (var ft in GetAllFilterTypes())
            {
                var attr = ft.GetCustomAttributes(typeof(FilterAttribute), false).FirstOrDefault() as FilterAttribute;
                if (attr != null)
                {
                    FilterTypes.Add(attr.TokenMatch, ft);
                }
            }
        }

        /// <summary>
        /// Get a specific filter type based on a token that might match it
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static Type GetFilterType(string token)
            => FilterTypes.FirstOrDefault(x => token.StartsWith(x.Key)).Value;

        /// <summary>
        /// Create a filter from a token
        /// </summary>
        /// <param name="token"></param>
        /// <returns>A filter or null if it cantc match the token to a type</returns>
        public static Filter CreateFilter(string token)
        {
            if (String.IsNullOrWhiteSpace(token)) return null;

            var t = GetFilterType(token);

            if ( t == null )
                throw new FilterReflectionFetchoException("Can't find filter for token type {0}", token);

            var method = t.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            if (method == null)
                throw new FilterReflectionFetchoException("Static Parse method is not declared on type {0}", t.Name);

            return method.Invoke(null, new[] { token }) as Filter;
        }

    }
}
