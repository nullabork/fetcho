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

        public DataHashFilter(string hash) : this()
            => Hash = hash.ToLower();

        private DataHashFilter()
            => CallOncePerPage = true;

        public override string GetQueryText()
            => string.Format("hash:{0}", Hash);

        public override string[] IsMatch(IWebResource resource, string fragment, Stream stream)
        {
            var hash = "";
            if (resource.PropertyCache.ContainsKey("datahash"))
                hash = resource.PropertyCache["datahash"].ToString().ToLower();
            else
            {
                hash = MD5Hash.Compute(stream).ToString();
                resource.PropertyCache.Add("datahash", MD5Hash.Compute(stream).ToString().ToLower());
            }

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
