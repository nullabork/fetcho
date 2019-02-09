using Fetcho.Common;
using System;
using System.IO;
using System.Text;
using System.Web;

namespace Fetcho.ContentReaders
{
    /// <summary>
    /// Extracts links from a text file
    /// </summary>
    public class TextFileLinkExtractor : ILinkExtractor
    {
        private TextReader reader;

        /// <summary>
        /// The current URI we're working on
        /// </summary>
        public Uri CurrentSourceUri { get; set; }

        public TextFileLinkExtractor(Uri sourceUri, TextReader reader)
        {
            this.reader = reader;
            this.CurrentSourceUri = sourceUri;
        }

        public Uri NextUri()
        {
            const string http_match = "http";
            int http_match_index = 0;

            const string href_match = "href=";
            int href_match_index = 0;

            var openquote = -1;
            var prevc = -1;
            var c = reader.Read();

            while (c > -1)
            {
                if (http_match[http_match_index] == c)
                    http_match_index++;
                else
                    http_match_index = 0;

                if (http_match_index == 1 && prevc > 0 && isOpenQuoteDelimeter((char)prevc)) openquote = prevc;

                if (http_match.Length == http_match_index)
                {
                    Uri uri = extractOneUri(http_match, openquote);
                    if (uri != null)
                        return uri;
                    http_match_index = 0;
                }

                if (href_match[href_match_index] == c)
                    href_match_index++;
                else
                    href_match_index = 0;

                if (href_match.Length == href_match_index)
                {
                    c = readPastWhitespace();

                    openquote = c;

                    if (c > 0)
                    {

                        prevc = c;
                        c = reader.Read();
                        string urlCandidate = readToNextQuote((char)openquote);
                        urlCandidate = HttpUtility.HtmlDecode(urlCandidate);
                        var urls = Utility.GetLinks(CurrentSourceUri, urlCandidate);

                        foreach (var url in urls)
                            return url;

                        href_match_index = 0;
                    }
                }

                prevc = c;
                c = reader.Read();
            }

            return null;
        }

        /// <summary>
        /// Try and extract one URI identified using the match string
        /// </summary>
        /// <param name="match">String that found the URI</param>
        /// <returns>A URI if it finds a valid URI or null if it finds nothing or an invalid URI</returns>
        /// <param name = "quotechar"></param>
        Uri extractOneUri(string match, int quotechar)
        {
            var sb = new StringBuilder();
            var i = reader.Peek();

            while (true)
            {
                i = reader.Peek();

                if (i < 0)
                    break;

                char c = (char)i;
                if ((quotechar >= 0 && isClosingQuoteDelimeter((char)quotechar, c)) || c == ' ' || c == '\'' || c == '\t' || c == '\n' || c == '\r' || c == '<')
                    break;

                reader.Read();
                sb.Append(c);
            }

            try
            {
                var uri = new Uri(match + HttpUtility.HtmlDecode(sb.ToString()));
                return uri;
            }
            catch (Exception)
            {
                return null;
            }
        }

        string readToNextQuote(char quoteChar)
        {
            var sb = new StringBuilder();
            int c = reader.Peek();
            if (c < 0) return "";

            c = reader.Read();
            while (c > 0 && (char)c != quoteChar)
            {
                sb.Append((char)c);
                c = reader.Read();
            }
            string s = sb.ToString();
            return s;
        }

        int readPastWhitespace()
        {
            int c = reader.Peek();
            while (c > 0 && char.IsWhiteSpace((char)c)) c = reader.Read();
            return c;
        }

        bool isOpenQuoteDelimeter(char c)
        {
            return c == '"' || c == '\'' || c == '(' || c == '[' || c == '{' || c == '<';
        }

        bool isClosingQuoteDelimeter(char openQuote, char prospectiveCloseQuote)
        {
            if (openQuote == '"' && prospectiveCloseQuote == '"') return true;
            if (openQuote == '\'' && prospectiveCloseQuote == '\'') return true;
            if (openQuote == '(' && prospectiveCloseQuote == ')') return true;
            if (openQuote == '[' && prospectiveCloseQuote == ']') return true;
            if (openQuote == '{' && prospectiveCloseQuote == '}') return true;
            if (openQuote == '<' && prospectiveCloseQuote == '>') return true;
            return false;
        }
    }


}
