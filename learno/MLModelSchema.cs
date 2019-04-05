using System.Collections.Generic;
using System.Linq;
using Fetcho.Common;

namespace learno
{
    public class MLModelSchema
    {
        public const int DefaultMinResultsPerCategory = 300;
        public const int DefaultMaxResultsPerCategory = 25000;

        /// <summary>
        /// Data sources to fetch and their name - note the datasource is the ultimate arbiter of category
        /// </summary>
        public Dictionary<string, MLModelSchemaCategoryDataSource> DataSource { get; }

        /// <summary>
        /// A list of categories to exclude from the final dataset 
        /// </summary>
        public List<string> CategoriesToExclude { get; }

        public string FilePath { get; set; }

        public bool OverwriteExistingModelFile { get; set; }

        public int MaxResultsPerCategory { get; set; }

        public int MinResultsPerCategory { get; set; }

        public bool BalanceResults { get; set; }

        public bool ThrowIfCategoryCountsOutsideRange { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }


        public MLModelSchema(string fileName)
        {
            DataSource = new Dictionary<string, MLModelSchemaCategoryDataSource>();
            CategoriesToExclude = new List<string>();
            OverwriteExistingModelFile = false;
            FilePath = fileName;
            MaxResultsPerCategory = DefaultMaxResultsPerCategory;
            MinResultsPerCategory = DefaultMinResultsPerCategory;
            BalanceResults = true;
            ThrowIfCategoryCountsOutsideRange = false;
        }

        /// <summary>
        /// Gets all the data for the data sources in the schema and applies any transforms applicable
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TextPageData> GetAllData()
        {
            var l = new List<TextPageData>();
            foreach (var kvp in DataSource)
            {
                // get the results for a category
                var results = kvp.Value.GetData(MaxResultsPerCategory);

                // throw an exception if we're outside the range
                if (ThrowIfCategoryCountsOutsideRange)
                {
                    if (results
                        .GroupBy(x => x.Category)
                        .Select(category => new { Category = category.Key, Count = category.Count() })
                        .Where(category => category.Count > MaxResultsPerCategory || category.Count < MinResultsPerCategory)
                        .Any())
                        throw new FetchoException("One or more categories has results outside the required range");
                }

                l.AddRange(results.Where(x => !CategoriesToExclude.Contains(x.Category)));
            }

            int maxResults = MaxResultsPerCategory;

            // balances by getting the min count and then setting that as the max for all categories
            if (BalanceResults)
            {
                maxResults = l
                .GroupBy(x => x.Category)
                .Select(category => new { Category = category.Key, Count = category.Count() })
                .Where(category => category.Count > MinResultsPerCategory)
                .Min(x => x.Count);
            }

            var final = new List<TextPageData>();

            // quitely take within the range and reject whole categories below the range
            foreach (var cat in l
                .GroupBy(x => x.Category)
                .Select(category => new { Category = category.Key, Count = category.Count() })
                .Where(category => category.Count > MinResultsPerCategory))
            {
                final.AddRange(l.Where(x => x.Category == cat.Category).Randomise().Take(maxResults));
            }

            return final;
        }

        /// <summary>
        /// Add a data source
        /// </summary>
        /// <param name="categoryDataSource"></param>
        public void AddDataSource(MLModelSchemaCategoryDataSource categoryDataSource)
            => DataSource.Add(categoryDataSource.Name, categoryDataSource);
    }
}
