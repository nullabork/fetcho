using System;
using System.IO;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    [Filter("xpath:", "xpath:[xpath|*][:xpath|*]", Hidden = true)]
    public class XPathFilter : Filter
    {
        public string XPath { get; set; }

        public override string Name => "XPath filter";

        public override bool RequiresStreamInput { get => true; }

        public override bool CallOncePerPage => true;

        public override bool IsReducingFilter => true;

        public XPathFilter(string xpath) 
            => XPath = xpath;

        public override string GetQueryText()
            => string.Format("xpath:{0}", XPath);

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            // all the approaches I've tried so far sucked or didnt work
            // need an XPath query library that can handle streamed HTML
            // and doesn't require perfect XML to work. HtmlAgilityPack is
            // the closest I've come to solving this problem but that required
            // the entire page to be stored in memory which is not workable here

            // pass out nothing until i get it working
            return EmptySet;
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

            return new XPathFilter(searchText);
        }

    }
}
