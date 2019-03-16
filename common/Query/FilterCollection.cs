using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    public class FilterCollection : IEnumerable<Filter>
    {
        private List<Filter> Filters { get; }

        public int Count { get => Filters.Count; }

        public FilterCollection()
        {
            Filters = new List<Filter>();
        }

        public void Add(Filter filter)
            => Filters.Add(filter);

        public void Remove(Filter filter)
            => Filters.Remove(filter);

        public void Clear() => Filters.Clear();

        public bool AllMatch(IWebResource resource, string fragment, Stream stream)
            => Filters
            .OrderBy(x => x.Cost)
            .All(x => x.IsMatch(resource, fragment, stream).Any());

        public bool AllMatch(IWebResource resource, string fragment, Stream stream, FilterCollectionMatchOptions options)
            => Filters
            .Where(x => !x.CallOncePerPage || options.RunCallOnceFilters)
            .OrderBy(x => x.Cost)
            .All(x => x.IsMatch(resource, fragment, stream).Any());

        public bool AnyMatch(IWebResource resource, string fragment, Stream stream)
            => Filters
            .OrderBy(x => x.Cost)
            .Any(x => x.IsMatch(resource, fragment, stream).Any());

        public bool AnyMatch(IWebResource resource, string fragment, Stream stream, FilterCollectionMatchOptions options)
            => Filters
            .Where(x => !x.CallOncePerPage || options.RunCallOnceFilters)
            .OrderBy(x => x.Cost)
            .Any(x => x.IsMatch(resource, fragment, stream).Any());
        
        public IEnumerable<string> GetTags(IWebResource resource, string fragment, Stream stream)
        {
            var l = new List<string>();
            foreach (var filter in Filters)
                l.AddRange(filter.IsMatch(resource, fragment, stream));
            return l.Distinct();
        }
        public IEnumerator<Filter> GetEnumerator()
            => Filters.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => Filters.GetEnumerator();
    }

    public class FilterCollectionMatchOptions
    {
        public bool RunCallOnceFilters = true;
    }
}
