using Fetcho.Common.QueryEngine;

namespace Fetcho.Common
{
    public interface ISubQuery
    {
        Query Query { get; set; }
    }
}
