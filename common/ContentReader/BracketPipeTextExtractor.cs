using BracketPipe;
using Fetcho.Common;
using System;
using System.Collections.Generic;
using System.IO;

namespace Fetcho.ContentReaders
{
    public class BracketPipeTextExtractor
    {
        private FastLookupCache<string> cache;

        public bool Distinct { get; set; }

        public ExtractionGranularity Granularity { get; set; }

        public int MinimumLength { get; set; }

        public int MaximumLength { get; set; }

        public bool StopWords { get; set; }

        public BracketPipeTextExtractor()
        {
            StopWords = false;
            MinimumLength = int.MinValue;
            MaximumLength = int.MaxValue;
            Granularity = ExtractionGranularity.Raw;
            Distinct = false;
        }

        public void Parse(Stream stream, Action<string> callback)
        {
            bool skip = false;

            if (Distinct) cache = new FastLookupCache<string>(10000);
            else cache = null;

            using (var reader = new HtmlReader(stream))
            {
                foreach (var c in reader)
                {
                    switch (c.Type)
                    {
                        case HtmlTokenType.StartTag:
                            if (c.Value == "script") skip = true;
                            if (c.Value == "style") skip = true;
                            break;

                        case HtmlTokenType.EndTag:
                            if (c.Value == "script") skip = false;
                            if (c.Value == "style") skip = false;
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
                                callback(c.Value);
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<string> ReadAllText(Stream stream)
        {
            var l = new List<string>();
            var parser = new BracketPipeTextExtractor();
            parser.Parse(stream, l.Add);
            return l;
        }
    }

    public enum ExtractionGranularity
    {
        Document,
        Paragraph,
        Keyword,
        Raw
    }
}
