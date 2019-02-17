using System.Net;

namespace Fetcho.Common
{
    /// <summary>
    /// Allows all content through
    /// </summary>
    public class NullBlockProvider : IBlockProvider
    {
        /// <summary>
        /// If everythig is OK
        /// </summary>
        const string OKBlockReason = "OK";

        public bool IsBlocked(WebRequest request, WebResponse resource, out string block_reason)
        {
            block_reason = OKBlockReason;

            return false;
        }

        /// <summary>
        /// A single version of this class
        /// </summary>
        public static readonly NullBlockProvider Default = new NullBlockProvider();
    }
}
