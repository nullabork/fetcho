using Antlr4.Runtime.Misc;
using Fetcho.Grammars.Html;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Fetcho.ContentReaders
{
    public class HTMLParserVisitor : HTMLParserBaseVisitor<string>
    {
        public override string VisitHtmlMisc([NotNull] HTMLParser.HtmlMiscContext context)
        {
            if (context.ChildCount == 0)
                Console.WriteLine(context.GetText());

            return base.VisitHtmlMisc(context);
        }
    }
}
