using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("filetype:", "filetype:[filetype|*][:filetype|*]")]
    public class FileTypeFilter : Filter
    {
        public string SearchText { get; set; }

        public override string Name => "File Type filter";

        public FileTypeFilter(string filetype) : this()
            => SearchText = filetype;

        private FileTypeFilter()
            => CallOncePerPage = true;

        public override string GetQueryText()
            => string.Format("filetype:{0}", SearchText);

        public override string[] IsMatch(IWebResource resource, string fragment, Stream stream)
        {
            string contentType = resource.RequestProperties.SafeGet("content-type");
            return String.IsNullOrWhiteSpace(SearchText) || contentType.Contains(SearchText) ? new string[1] { Utility.MakeTag(contentType) } : EmptySet;
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

            return new FileTypeFilter(searchText);
        }
    }
}
