using System.Net;

namespace Fetcho.Common
{
    public class DefaultBlockProvider : IBlockProvider
    {
        /// <summary>
        /// Some sites will provide this string in their headers as 'Rating:'
        /// </summary>
        const string AdultRatingString = "RTA-5042-1996-1400-1577-RTA";

        /// <summary>
        /// If everythig is OK
        /// </summary>
        const string OKBlockReason = "OK";
        
        public int MaxFileDownloadLengthInBytes { get; set; }

        public DefaultBlockProvider()
        {
            MaxFileDownloadLengthInBytes = Settings.MaxFileDownloadLengthInBytes;
        }

        public bool IsBlocked(WebRequest request, WebResponse response, out string block_reason)
        {
            block_reason = OKBlockReason;
            if (IsHttpRequest(request))
                return IsHttpBlocked(request as HttpWebRequest, response as HttpWebResponse, out block_reason);
            return true;
        }

        private bool IsHttpRequest(WebRequest request) => request as HttpWebRequest != null;

        private bool IsHttpBlocked(HttpWebRequest request, HttpWebResponse response, out string block_reason)
        {
            block_reason = OKBlockReason;
            bool rtn = true;

            if (response.ContentLength > Settings.MaxFileDownloadLengthInBytes)
            {
                block_reason = "Response exceeded max length of " + MaxFileDownloadLengthInBytes;
            }
            else if (response.Headers["Rating"] == AdultRatingString)
            {
                block_reason = "Adult rated site. Not continuing with download.";
            }
            // block image/video/audio since we can't do anything with it
            else if (response.ContentType.StartsWith("image/") ||
                     response.ContentType.StartsWith("video/") ||
                     response.ContentType.StartsWith("audio/"))
            {
                block_reason = "Content type '" + response.ContentType + "' is blocked from downloading";
            }
            else
            {
                rtn = false;
            }

            return rtn;
        }
    }
}
