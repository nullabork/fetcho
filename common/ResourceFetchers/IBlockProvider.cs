using System.Net;

namespace Fetcho.Common
{
    public interface IBlockProvider
    {

        /// <summary>
        /// Returns true if the provided request/response should be blocked
        /// </summary>
        /// <param name="request"></param>
        /// <param name="resource"></param>
        /// <param name="blockReason"></param>
        /// <returns></returns>
        bool IsBlocked(WebRequest request, WebResponse resource, out string blockReason);
    }
}
