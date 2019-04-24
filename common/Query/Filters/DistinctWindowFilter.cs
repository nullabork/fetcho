using System;
using System.IO;
using System.Linq;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    /// <summary>
    /// Simple text match filter to include results
    /// </summary>
    [Filter("distinct-window(", "distinct-window([domain|ip|title|referer|datahash]):window_size_in_pages",
        Description = "Number of distinct pages to show before potentially including a duplicate")]
    public class DistinctWindowFilter : Filter
    {
        // Memory use will be related to this
        public const int MaxWindowSize = 1000;
        public const string DistinctWindowFilterKey = "distinct-window(";

        private FastLookupCache<string> seenWindow = null;

        /// <summary>
        /// Size of the distinct window
        /// </summary>
        public int WindowSize { get; set; }

        /// <summary>
        /// Field to confirm distinctness for
        /// </summary>
        public string FieldName { get; set; }

        /// <summary>
        /// Name of this filter
        /// </summary>
        public override string Name { get => "Distinct Window"; }

        // this needs to be run last - its actually a cheap query
        public override decimal Cost => MaxCost;

        public override bool RequiresResultInput { get => true; }

        public override bool IsReducingFilter => true;

        /// <summary>
        /// Constructor
        /// </summary>
        public DistinctWindowFilter(string fieldName, int windowSize)
        {
            ThrowIfInvalidFieldName(fieldName);
            FieldName = fieldName;
            WindowSize = windowSize.ConstrainRange(1,MaxWindowSize);
            seenWindow = new FastLookupCache<string>(windowSize);
        }

        /// <summary>
        /// Can't create using the default constructor
        /// </summary>
        private DistinctWindowFilter() { }

        /// <summary>
        /// Try and match the fragment from the file
        /// </summary>
        /// <param name="fragment"></param>
        /// <returns></returns>
        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            string value = GetFieldValue(FieldName, result);

            if (seenWindow.Contains(value))
                return EmptySet;
            else
            {
                seenWindow.Enqueue(value);
                return new string[1];
            }

        }

        /// <summary>
        /// Output as string
        /// </summary>
        /// <returns></returns>
        public override string GetQueryText()
            => string.Format("{0}{1}):{2}", DistinctWindowFilterKey, FieldName, WindowSize);


        public static Filter Parse(string queryText, int depth)
        {
            //try
            //{
            int windowSize = MaxWindowSize;

            var tokens = queryText.Split(':');
            if (tokens.Length != 2) return null;

            var distinctField = tokens[0].Substring(
                DistinctWindowFilterKey.Length,
                tokens[0].Length - DistinctWindowFilterKey.Length - 1);

            if (int.TryParse(tokens[1], out windowSize))
                windowSize = MaxWindowSize;

            return new DistinctWindowFilter(distinctField, windowSize);
            //}
            //catch (Exception ex)
            //{
            //    Utility.LogException(ex);
            //    return null;
            //}
        }

        private string GetFieldValue(string fieldName, WorkspaceResult result)
        {
            if (fieldName == "domain")
                return new Uri(result.Uri).Host;
            if (fieldName == "host")
                return new Uri(result.Uri).Host;
            if (fieldName == "ip")
                return Utility.GetHostIPAddress(new Uri(result.Uri)).ToString();
            if (fieldName == "title")
                return result.Title;
            if (fieldName == "referer")
                return result.RefererUri;
            if (fieldName == "datahash")
                return result.DataHash;
            if (fieldName == "tags")
                return result.Tags.Aggregate("", (x, y) => x + y);
            return string.Empty;
        }

        private void ThrowIfInvalidFieldName(string fieldName)
        {
            string[] fields =
            {
                "domain",
                "host",
                "ip",
                "title",
                "referer",
                "datahash",
                "tags"
            };

            if (fields.Count(x => x == fieldName) == 0)
                throw new FetchoException("Invalid field {0}", fieldName);
        }


    }


}
