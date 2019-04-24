
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Fetcho.Common.Entities;
using Fetcho.Common.QueryEngine;

namespace Fetcho.Common
{
    public abstract class Filter
    {
        public const decimal MaxCost = 9999999m;
        public const string WildcardChar = "*";

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
        public virtual bool CallOncePerPage { get => false; }

        /// <summary>
        /// Returns true if this filter uses the result input of IsMatch
        /// </summary>
        public virtual bool RequiresResultInput { get => false; }

        /// <summary>
        /// Returns true if this filter uses the text input of IsMatch
        /// </summary>
        public virtual bool RequiresTextInput { get => false; }

        /// <summary>
        /// Returns true if this filter uses the stream input of IsMatch
        /// </summary>
        public virtual bool RequiresStreamInput { get => false; }

        /// <summary>
        /// True if this filter is a sub query filter
        /// </summary>
        public virtual bool IsSubQuery { get => false; }

        /// <summary>
        /// True if this filter can be used to determine if a result should be included or excluded or whether it'll always pass all values
        /// </summary>
        /// ie. is it a useless filter?
        public virtual bool IsReducingFilter { get => false; }

        /// <summary>
        /// Amount to adjust the cost by depending on different types of filter modes
        /// </summary>
        public virtual decimal FilterModeCostAdjustmentFactor { get => FilterMode == FilterMode.Include ? 1 : 1000; }

        /// <summary>
        /// Whether this filter is deciding to include, exclude or tag items
        /// </summary>
        public virtual FilterMode FilterMode { get; set; }

        /// <summary>
        /// If the resource is matched by this filter
        /// </summary>
        public abstract string[] IsMatch(WorkspaceResult result, string fragment, Stream stream);


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
        {
            try
            {
                var types = typeof(Filter).Assembly.GetTypes().Where(x => x.IsSubclassOf(typeof(Filter)));
                return types;
            }
            catch (ReflectionTypeLoadException rtlex)
            {
                Utility.LogException(rtlex.LoaderExceptions[0]);
            }
            catch (Exception)
            {
                throw;
            }

            return null;
        }

        /// <summary>
        /// Cache of all the filter types
        /// </summary>
        public static Dictionary<string, Type> FilterTypes = null;

        /// <summary>
        /// Create the cache of filter types
        /// </summary>
        private static void InitaliseFilterTypes()
        {
            if (FilterTypes != null) return;
            lock (_filterTypesLock)
            {
                if (FilterTypes == null)
                {
                    FilterTypes = new Dictionary<string, Type>();
                    foreach (var ft in GetAllFilterTypes())
                    {
                        var attr = ft.GetCustomAttributes(typeof(FilterAttribute), false).FirstOrDefault() as FilterAttribute;
                        if (attr != null)
                        {
                            FilterTypes.Add(attr.TokenMatch, ft);
                        }
                    }
                }
            }
        }

        private static readonly object _filterTypesLock = new object();

        /// <summary>
        /// Get a specific filter type based on a token that might match it
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static Type GetFilterType(string token)
        {
            InitaliseFilterTypes();
            return FilterTypes.FirstOrDefault(x => token.StartsWith(x.Key)).Value;
        }

        /// <summary>
        /// Create a filter from a token
        /// </summary>
        /// <param name="token"></param>
        /// <returns>A filter or null if it cantc match the token to a type</returns>
        public static Filter CreateFilter(string token, int depth)
        {
            try
            {

                if (string.IsNullOrWhiteSpace(token)) return null;

                var t = GetFilterType(token);

                if (t == null)
                    throw new FilterReflectionFetchoException("Can't find filter for token type {0}", token);

                var method = t.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                if (method == null)
                    throw new FilterReflectionFetchoException("Static Parse method is not declared on type {0}", t.Name);

                var filter = method.Invoke(null, new object[] { token, depth }) as Filter;
                return filter;
            }
            catch (TargetInvocationException ex)
            {
                Utility.LogException(ex);
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Determines whether this filter is running in specific FilterMode(s)
        /// </summary>
        /// <param name="filterMode"></param>
        /// <returns></returns>
        public bool HasFilterMode(FilterMode filterMode)
            => (FilterMode & filterMode) != FilterMode.None;

        /// <summary>
        /// No matches
        /// </summary>
        /// <remarks>Since we'll be returning lots of these lets reduce the footprint here by using one object to represent all of them</remarks>
        public static readonly string[] EmptySet = new string[0];
    }
}
