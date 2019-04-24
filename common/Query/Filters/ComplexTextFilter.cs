using System;
using System.Collections.Generic;
using System.IO;
using Fetcho.Common.Entities;
using Fetcho.ContentReaders;

namespace Fetcho.Common
{
    [Filter("text:", "text:[word|*][:word|*]")]
    public class ComplexTextFilter : Filter
    {
        public string Word { get; set; }

        public override string Name => "Complex Text Filter";

        public ComplexTextFilter(string word)
            => Word = word;

        public override decimal Cost => 100m;

        public override bool CallOncePerPage => true;

        public override bool IsReducingFilter => !string.IsNullOrWhiteSpace(Word);

        public override bool RequiresStreamInput { get => true; }

        public override string GetQueryText()
            => string.Format("text:{0}", Word);

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            var parser = new BracketPipeTextExtractor()
            {
                Distinct = false,
                Filter = BracketPipeTextExtractorFilterType.Core,
                Granularity = ExtractionGranularity.Raw,
                MinimumLength = 0,
                MaximumLength = int.MaxValue,
                StopWords = false
            };

            var l = new List<BracketPipeTextFragment>();
            parser.Parse(stream, l.Add);

            foreach (var f in l)
                if (f.Text.IndexOf(Word, StringComparison.InvariantCultureIgnoreCase) > -1)
                    return new string[1];

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
                if (searchText == WildcardChar) searchText = string.Empty;
            }

            return new ComplexTextFilter(searchText);
        }
    }
}
