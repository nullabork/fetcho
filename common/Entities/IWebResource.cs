using System.Collections.Generic;

namespace Fetcho.Common.Entities
{
    public interface IWebResource
    {
        Dictionary<string, string> RequestProperties { get; }
        Dictionary<string, string> ResponseProperties { get; }
        Dictionary<string, object> PropertyCache { get;  }
    }
}
