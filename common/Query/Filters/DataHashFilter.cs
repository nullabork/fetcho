using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("hash:", "hash:[md5_hash|*][:md5_hash|*]")]
    public class DataHashFilter : Filter
    {
        public string Hash { get; set; }

        public override string Name 
            => "Data Hash Filter";

        public override bool RequiresResultInput { get => true; }

        public override bool CallOncePerPage => false;

        public override bool IsReducingFilter => !string.IsNullOrWhiteSpace(Hash);

        public DataHashFilter(string hash) 
            => Hash = hash.ToLower();

        public override string GetQueryText()
            => string.Format("hash:{0}", Hash);

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            var hash = "";
            if (!string.IsNullOrWhiteSpace(result.DataHash))
                hash = result.DataHash;

            if (hash.Equals(Hash, StringComparison.InvariantCultureIgnoreCase) || string.IsNullOrWhiteSpace(Hash)) return new string[1] { hash };
            return EmptySet;
        }

        /// <summary>
        /// Parse some text to create this object
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText, int depth)
        {
            string searchText = string.Empty;

            int index = queryText.IndexOf(':');
            if (index > -1)
            {
                searchText = queryText.Substring(index + 1);
                if (searchText == "*") searchText = string.Empty;
            }

            return new DataHashFilter(searchText);
        }
    }
}
