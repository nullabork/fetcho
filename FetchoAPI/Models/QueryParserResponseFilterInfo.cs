using Fetcho.Common.QueryEngine;

namespace Fetcho.FetchoAPI.Controllers
{
    public class QueryParserResponseFilterInfo
    {
        public string Type { get; set; }

        public decimal Cost { get; set; }

        public FilterMode Mode { get; set; }

        public string FilterText { get; set; }

        public bool RequiresResultInput { get; set; }

        public bool RequiresTextInput { get; set; }

        public bool RequiresStreamInput { get; set; }

        public QueryParserResponse SubQueryDetails { get; set; }
    }

}
