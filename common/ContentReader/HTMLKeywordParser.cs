using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Fetcho.Common;
using Fetcho.Grammars.Html;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace Fetcho.ContentReaders
{
    internal class HTMLKeywordParserListener : HTMLParserBaseListener
    {
        public Action<string> Emit { get; set; }

        public int MinimumLength { get; set; }

        public int MaximumLength { get; set; }

        public bool IncludeComments { get; set; }

        public bool IncludeChardata { get; set; }

        public HTMLKeywordParserListener()
        {
            MinimumLength = int.MinValue;
            MaximumLength = int.MaxValue;
            IncludeChardata = true;
            IncludeComments = true;
        }

        public override void EnterHtmlChardata([NotNull] HTMLParser.HtmlChardataContext context)
        {
            if (!IncludeChardata) return;
            var sb = new StringBuilder(context.GetText().Trim());
            if (sb.Length.Between(MinimumLength, MaximumLength))
                Emit(HttpUtility.HtmlDecode(sb.ToString()));
        }

        public override void EnterHtmlComment([NotNull] HTMLParser.HtmlCommentContext context)
        {
            if (!IncludeComments) return;
            var sb = new StringBuilder(context.GetText().Trim());
            if (sb.Length.Between(MinimumLength, MaximumLength))
                Emit(HttpUtility.HtmlDecode(sb.ToString()));
        }

    }

    public class HTMLKeywordExtractor
    {
        public int MinimumLength { get; set; }

        public int MaximumLength { get; set; }

        public bool IncludeComments { get; set; }

        public bool IncludeChardata { get; set; }

        public HTMLKeywordExtractor()
        {
            MinimumLength = int.MinValue;
            MaximumLength = int.MaxValue;
            IncludeComments = true;
            IncludeChardata = true;
        }

        public void Parse(Stream stream, Action<string> callback)
        {
            var inputStream = new AntlrInputStream(stream);
            var lexer = new HTMLLexer(inputStream);
            lexer.RemoveErrorListeners();
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new HTMLParser(tokenStream);
            parser.RemoveErrorListeners();
            var context = parser.htmlDocument();
            var listener = new HTMLKeywordParserListener()
            {
                Emit = (x) => callback(x),
                MinimumLength = MinimumLength,
                MaximumLength = MaximumLength,
                IncludeChardata = IncludeChardata,
                IncludeComments = IncludeComments
            };
            var walker = new ParseTreeWalker();
            walker.Walk(listener, context);

        }


    }

}
