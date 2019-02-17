using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Fetcho.Common
{
    public class FilterCollection : IEnumerable<IFilter>
    {
        private List<IFilter> Filters { get; }

        public int Count { get => Filters.Count; }

        public FilterCollection()
        {
            Filters = new List<IFilter>();
        }

        public void Add(IFilter filter) => Filters.Add(filter);

        public void Remove(IFilter filter) => Filters.Remove(filter);

        public void Clear() => Filters.Clear();

        public bool AllMatch(Uri uri, string fragment) => Filters.All(x => x.IsMatch(uri, fragment));

        public bool AnyMatch(Uri uri, string fragment) => Filters.Any(x => x.IsMatch(uri, fragment));

        public IEnumerator<IFilter> GetEnumerator() => Filters.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Filters.GetEnumerator();
    }
}
