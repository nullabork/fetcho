﻿using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("filetype:", "filetype:[filetype|*][:filetype|*]")]
    public class FileTypeFilter : Filter
    {
        public string SearchText { get; set; }

        public override string Name => "File Type filter";

        public override bool RequiresResultInput { get => true; }

        public override bool CallOncePerPage => true;

        public override bool IsReducingFilter => !string.IsNullOrWhiteSpace(SearchText);

        public FileTypeFilter(string filetype) 
            => SearchText = filetype;

        public override string GetQueryText()
            => string.Format("filetype:{0}", SearchText);

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            string contentType = result.ResponseProperties.SafeGet("content-type") ?? string.Empty;
            return string.IsNullOrWhiteSpace(SearchText) || contentType.Contains(SearchText) ? new string[1] { Utility.MakeTag(contentType) } : EmptySet;
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
                if (searchText == WildcardChar) searchText = string.Empty;
            }

            return new FileTypeFilter(searchText);
        }
    }
}
