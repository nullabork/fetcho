using System.Collections.Generic;
using System.Linq;

namespace Fetcho.Common
{
    public class FilterCollection
    {
        private List<IFilter> Filters { get; }

        public FilterCollection()
        {
            Filters = new List<IFilter>();
        }

        public void Add(IFilter filter) => Filters.Add(filter);

        public void Remove(IFilter filter) => Filters.Remove(filter);

        public void Clear() => Filters.Clear();

        public bool AllMatch(string fragment) => Filters.All(x => x.IsMatch(fragment));

        public bool AnyMatch(string fragment) => Filters.Any(x => x.IsMatch(fragment));


    }
}
