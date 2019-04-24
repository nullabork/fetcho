using BracketPipe;
using Fetcho.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Fetcho.ContentReaders
{
    public class BracketPipeTextExtractor
    {
        public const int CacheSize = 10000;
        public const string ScriptHtmlTag = "script";
        public const string StyleHtmlTag = "style";

        private FastLookupCache<string> cache;

        public bool Distinct { get; set; }

        public ExtractionGranularity Granularity { get; set; }

        public int MinimumLength { get; set; }

        public int MaximumLength { get; set; }

        public bool StopWords { get; set; }

        public BracketPipeTextExtractorFilterType Filter { get; set; }

        public BracketPipeTextExtractor()
        {
            StopWords = false;
            MinimumLength = int.MinValue;
            MaximumLength = int.MaxValue;
            Granularity = ExtractionGranularity.Raw;
            Distinct = false;
            Filter = BracketPipeTextExtractorFilterType.Raw; 
        }

        public void Parse(Stream stream, Action<BracketPipeTextFragment> callback)
        {
            bool skip = false;

            if (Distinct) cache = new FastLookupCache<string>(CacheSize);
            else cache = null;

            var filter = GetFilter(Filter);
            if (filter == null)
                throw new FetchoException("{0} is an invalid filter type", Filter);

            var tag = new Stack<string>();

            using (var reader = new HtmlReader(stream))
            {
                foreach (var c in reader)
                {
                    switch (c.Type)
                    {
                        case HtmlTokenType.StartTag:
                            if (c.Value == ScriptHtmlTag) skip = true;
                            if (c.Value == StyleHtmlTag) skip = true;
                            tag.Push(c.Value);
                            break;

                        case HtmlTokenType.EndTag:
                            if (c.Value == ScriptHtmlTag) skip = false;
                            if (c.Value == StyleHtmlTag) skip = false;
                            if (tag.Count > 0) tag.Pop();
                            break;

                        default:
                            break;
                    }

                    if (!skip && c.Type == HtmlTokenType.Text)
                    {
                        if (c.Value.Length.IsBetween(MinimumLength, MaximumLength))
                        {
                            if (cache == null || !cache.Contains(c.Value))
                            {
                                cache?.Enqueue(c.Value);
                                string tagvalue = string.Empty;
                                if ( tag.Count > 0 ) tagvalue = tag.Peek();
                                var fragment = new BracketPipeTextFragment(tagvalue, c.Value);
                                if ( filter(fragment)) callback(fragment);
                            }
                        }
                    }
                }
            }
        }

        public Func<BracketPipeTextFragment, bool> GetFilter(BracketPipeTextExtractorFilterType filterType)
        {
            switch(filterType)
            {
                case BracketPipeTextExtractorFilterType.Core:
                    return (x) => GenericTagFilter(CoreTags, x); 
                case BracketPipeTextExtractorFilterType.NonCore:
                    return (x) => !GenericTagFilter(CoreTags, x);
                case BracketPipeTextExtractorFilterType.Links:
                    return (x) => GenericTagFilter(LinkTags, x);
                case BracketPipeTextExtractorFilterType.Headers:
                    return (x) => GenericTagFilter(HeaderTags, x);
                case BracketPipeTextExtractorFilterType.Time:
                    return (x) => GenericTagFilter(TimeTags, x);
                case BracketPipeTextExtractorFilterType.Raw:
                default:
                    return (x) => true;
            }
        }

        private static readonly string[] CoreTags = { "p", "h1", "h2", "strong", "div" };
        private static readonly string[] HeaderTags = { "h1", "h2", "h3", "h4" };
        private static readonly string[] LinkTags = { "a" };
        private static readonly string[] TimeTags = { "time" };

        private bool GenericTagFilter(string[] validTags, BracketPipeTextFragment fragment)
            => validTags.Any(x => x == fragment.Tag);

        public static IEnumerable<BracketPipeTextFragment> ReadAllText(Stream stream)
        {
            var l = new List<BracketPipeTextFragment>();
            var parser = new BracketPipeTextExtractor();
            parser.Parse(stream, l.Add);
            return l;
        }
    }
}
