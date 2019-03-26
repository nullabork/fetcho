using System.Collections.Generic;
using System.Linq;
using Fetcho.Common;

namespace learno
{
    public class MLModelSchema
    {
        public const int DefaultMaxResultsPerWorkspace = 5000;

        public Dictionary<string, MLModelSchemaCategoryDataSource> Categories { get; }

        public string FilePath { get; set; }

        public bool OverwriteExistingModelFile { get; set; }

        public int MaxResultsPerDataSource { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public MLModelSchema(string fileName)
        {
            Categories = new Dictionary<string, MLModelSchemaCategoryDataSource>();
            OverwriteExistingModelFile = false;
            FilePath = fileName;
            MaxResultsPerDataSource = DefaultMaxResultsPerWorkspace;
        }

        public IEnumerable<TextPageData> GetAllData()
        {
            var l = new List<TextPageData>();
            foreach (var kvp in Categories)
            {
                var results = kvp.Value.GetData(MaxResultsPerDataSource);

                if (results
                    .GroupBy( x => x.Category)
                    .Select( category => new { Category = category.Key, Count = category.Count() })
                    .Where( category => category.Count > MaxResultsPerDataSource)
                    .Any())
                    throw new FetchoException("Too many results returned for MaxResultsPerDataSource");

                l.AddRange(results);
            }
            return l;
        }

        public void AddCategory(MLModelSchemaCategoryDataSource categoryDataSource)
            => Categories.Add(categoryDataSource.Category, categoryDataSource);
    }
}
