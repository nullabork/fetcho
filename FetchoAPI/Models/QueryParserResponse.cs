using Fetcho.Common;
using Fetcho.Common.QueryEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Fetcho.FetchoAPI.Controllers
{
    /// <summary>
    /// Contains details of an attempt to parse a query
    /// </summary>
    public class QueryParserResponse
    {
        /// <summary>
        /// Original text parsed
        /// </summary>
        public string OriginalQueryText { get; set; }

        /// <summary>
        /// Query text after reducing for duplicate and redundant filters
        /// </summary>
        public string ParsedQueryText { get; set; }

        /// <summary>
        /// The original query split into tokens for turning into filters
        /// </summary>
        public List<string> Tokens { get; set; }

        /// <summary>
        /// Details about each filter in the query text
        /// </summary>
        public List<QueryParserResponseFilterInfo> Filters { get; set; }

        /// <summary>
        /// Did the parsing succeed?
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Why did the parsing fail?
        /// </summary>
        public string ErrorReason { get; set; }

        /// <summary>
        /// Aggregate cost of the filters
        /// </summary>
        public decimal? EstimatedMaxCost { get; set; }

        public bool RequiresResultInput { get; set; }

        public bool RequiresTextInput { get; set; }

        public bool RequiresStreamInput { get; set; }

        public QueryParserResponse()
        {
            Filters = null;
            EstimatedMaxCost = null;
            ErrorReason = String.Empty;
            Success = false;
            ParsedQueryText = String.Empty;
            OriginalQueryText = String.Empty;
            Tokens = new List<string>();
        }

        public void AddFilter(Filter f)
            => Filters.Add(new QueryParserResponseFilterInfo
            {
                Type = f.GetType().Name,
                Cost = f.Cost,
                FilterText = f.GetQueryText(),
                RequiresResultInput = f.RequiresResultInput,
                RequiresTextInput = f.RequiresTextInput,
                RequiresStreamInput = f.RequiresStreamInput,
                SubQueryDetails = f.IsSubQuery ? QueryParserResponse.Create((f as ISubQuery).Query.OriginalQueryText) : null
            });

        /// <summary>
        /// Create a QueryParserResponse from a query text string
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static QueryParserResponse Create(string queryText)
        {
            var r = new QueryParserResponse();

            r.OriginalQueryText = queryText;

            try
            {
                var query = new Query(queryText);

                r.Tokens.AddRange(Query.TokeniseQueryText(queryText));
                r.Filters = new List<QueryParserResponseFilterInfo>();
                foreach (var f in query.Filters.OrderBy(x => x.Cost * x.FilterModeCostAdjustmentFactor))
                    r.AddFilter(f);
                foreach (var f in query.Taggers.OrderBy(x => x.Cost))
                    r.AddFilter(f);

                r.ParsedQueryText = query.ToString();

                r.EstimatedMaxCost = query.Filters.Aggregate(0m, (x, y) => x + y.Cost * y.FilterModeCostAdjustmentFactor);
                r.EstimatedMaxCost += query.Taggers.Aggregate(0m, (x, y) => x + y.Cost);

                r.RequiresResultInput = query.RequiresResultInput;
                r.RequiresTextInput = query.RequiresTextInput;
                r.RequiresStreamInput = query.RequiresStreamInput;
                r.Success = true;
            }
            catch (Exception ex)
            {
                r.Success = false;
                r.ErrorReason = ex.Message;
                Utility.LogException(ex);
            }

            return r;
        }
    }

    public class QueryParserResponseFilterInfo
    {
        public string Type { get; set; }

        public decimal Cost { get; set; }

        public string FilterText { get; set; }

        public bool RequiresResultInput { get; set; }

        public bool RequiresTextInput { get; set; }

        public bool RequiresStreamInput { get; set; }

        public QueryParserResponse SubQueryDetails { get; set; }
    }

}
