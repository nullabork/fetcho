using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

        public bool AllMatch(Uri uri, string fragment) 
            => Filters.All(x => x.IsMatch(uri, fragment).Any());

        public bool AnyMatch(Uri uri, string fragment) 
            => Filters.Any(x => x.IsMatch(uri, fragment).Any());

        public IEnumerable<string> GetTags(Uri uri, string fragment)
        {
            var l = new List<string>();
            foreach (var filter in Filters)
                l.AddRange(filter.IsMatch(uri, fragment));
            return l;
        }
        public IEnumerator<Filter> GetEnumerator() 
            => Filters.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() 
            => Filters.GetEnumerator();
    }
}
