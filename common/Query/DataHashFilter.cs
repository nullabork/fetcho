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

        public override bool IsReducingFilter => !String.IsNullOrWhiteSpace(Hash);

        public DataHashFilter(string hash) 
            => Hash = hash.ToLower();

        public override string GetQueryText()
            => string.Format("hash:{0}", Hash);

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            var hash = "";
            if (!String.IsNullOrWhiteSpace(result.DataHash))
                hash = result.DataHash;

            if (hash.Equals(Hash, StringComparison.InvariantCultureIgnoreCase) || String.IsNullOrWhiteSpace(Hash)) return new string[1] { hash };
            return EmptySet;
        }

        /// <summary>
        /// Parse some text to create this object
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText)
        {
            string searchText = String.Empty;

            int index = queryText.IndexOf(':');
            if (index > -1)
            {
                searchText = queryText.Substring(index + 1);
                if (searchText == "*") searchText = String.Empty;
            }

            return new DataHashFilter(searchText);
        }
    }
}
