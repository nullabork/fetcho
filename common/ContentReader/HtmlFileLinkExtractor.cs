using BracketPipe;
using Fetcho.Common;
using System;
using System.IO;
using System.Linq;

namespace Fetcho.ContentReaders
{
    public class HtmlFileLinkExtractor : ILinkExtractor
    {
        private HtmlReader reader;
        public Uri CurrentSourceUri { get; set; }

        public HtmlFileLinkExtractor(Uri currentSourceUri, Stream reader)
        {
            this.reader = new HtmlReader(reader);
            this.CurrentSourceUri = currentSourceUri;
        }

        public void Dispose()
        {
            this.reader?.Dispose();
            this.reader = null;
        }

        public Uri NextUri()
        {
            Uri uri = null;

            while (!reader.EOF && uri == null )
            {
                var node = reader.NextNode();

                if (node.Value == "a")
                {
                    var href = reader.GetAttribute("href");
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        var links = Utility.GetLinks(CurrentSourceUri, href);
                        if (links.Any())
                            uri = links.First();
                    }
                }
                else if ( node.Value == "script")
                {
                    var href = reader.GetAttribute("src");
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        var links = Utility.GetLinks(CurrentSourceUri, href);
                        if (links.Any())
                            uri = links.First();
                    }
                }
                else if ( node.Value == "link")
                {
                    var href = reader.GetAttribute("href");
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        var links = Utility.GetLinks(CurrentSourceUri, href);
                        if (links.Any())
                            uri = links.First();
                    }
                }
                else if (node.Value == "base")
                {
                    var href = reader.GetAttribute("href");
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        // sometimes the links are bogus?!
                        var l = Utility.GetLinks(null, href);
                        if (l.Any())
                        {
                            CurrentSourceUri = l.First();
                        }
                    }
                }
            }

            return uri;
        }
    }


}
