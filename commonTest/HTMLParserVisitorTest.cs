using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Fetcho.Grammars.Html;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class HTMLParserVisitorTest
    {
        internal class HTMLVisitor : HTMLParserBaseVisitor<string>
        {
            bool inTitle = false;

            public override string VisitHtmlChardata([NotNull] HTMLParser.HtmlChardataContext context)
            {
                // this should just extract out the text of the HTML
                if ( inTitle)
                Console.WriteLine(context.HTML_TEXT());
                return base.VisitHtmlChardata(context);
            }

            public override string VisitHtmlTagName([NotNull] HTMLParser.HtmlTagNameContext context)
            {
                inTitle = context.TAG_NAME().ToString().ToLower() == "title";
                return base.VisitHtmlTagName(context);
            }
        }

        [TestMethod]
        public void VisitorTest()
        {
            try
            {
                string input = "<html><title>blahg blah blah</title><p>para graph</p><p>second para</p></html>";
                StringBuilder text = new StringBuilder(input);

                Console.WriteLine(input);
                
                var inputStream = new AntlrInputStream(text.ToString());
                var htmlLexer = new HTMLLexer(inputStream);
                var commonTokenStream = new CommonTokenStream(htmlLexer);
                var htmlParser = new HTMLParser(commonTokenStream);
                var htmlContext = htmlParser.htmlDocument();
                var visitor = new HTMLVisitor();
                visitor.Visit(htmlContext);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex);
            }
        }
    }
}
