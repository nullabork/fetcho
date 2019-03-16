using System;
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

        public DefaultBlockProvider()
        {
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

            if (response.ContentLength > FetchoConfiguration.Current.MaxFileDownloadLengthInBytes)
            {
                block_reason = "Response exceeded max length of " + FetchoConfiguration.Current.MaxFileDownloadLengthInBytes;
            }
            else if (response.Headers["Rating"] == AdultRatingString)
            {
                block_reason = "Adult rated site. Not continuing with download.";
            }
            // block image/video/audio since we can't do anything with it
            else if (response.ContentType.StartsWith("image/") ||
                     response.ContentType.StartsWith("video/") ||
                     response.ContentType.StartsWith("binary/") ||
                     response.ContentType.StartsWith("application/vnd.") ||
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

        /// <summary>
        /// Determines if this block provider will probably block this URL
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        /// <remarks>This is a cheap version of the provider without fetching the content</remarks>
        public static bool IsProbablyBlocked(Uri uri) => 
            uri.AbsolutePath.ToString().EndsWith(".jpg") ||
            uri.AbsolutePath.ToString().EndsWith(".jpeg") ||
            uri.AbsolutePath.ToString().EndsWith(".gif") ||
            uri.AbsolutePath.ToString().EndsWith(".png") ||
            uri.AbsolutePath.ToString().EndsWith(".ico") ||
            uri.AbsolutePath.ToString().EndsWith(".svg") ||
            uri.AbsolutePath.ToString().EndsWith(".avi") ||
            uri.AbsolutePath.ToString().EndsWith(".mp4") ||
            uri.AbsolutePath.ToString().EndsWith(".mp3") ||
            uri.AbsolutePath.ToString().EndsWith(".wav");
    }
}
