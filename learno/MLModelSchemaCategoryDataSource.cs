using System.Collections.Generic;

namespace learno
{
    public abstract class MLModelSchemaCategoryDataSource
    {
        public string Category { get; set; }

        public abstract IEnumerable<TextPageData> GetData(int maxRecords);
    }
}
