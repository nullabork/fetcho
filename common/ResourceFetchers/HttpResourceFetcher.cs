/*
 * Created by SharpDevelop.
 * User: alistair
 * Date: 29/08/2015
 * Time: 2:36 PM
 */
using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Threading;

using System.Threading.Tasks;
using Fetcho.Common.entities;
using log4net;

namespace Fetcho.Common
{
    public class HttpResourceFetcher : ResourceFetcher
    {
        static readonly ILog log = LogManager.GetLogger(typeof(HttpResourceFetcher));

        /// <summary>
        /// Some sites will provide this string in their headers as 'Rating:'
        /// </summary>
        const string AdultRatingString = "RTA-5042-1996-1400-1577-RTA";

        /// <summary>
        /// Fetches a copy of a HTTP resource
        /// </summary>
        /// <param name="uri"></param>
        /// <param name = "writeStream"></param>
        /// <param name="lastFetchedDate">Date we last fetched the resource - helps in optimising resources</param>
        /// <returns></returns>
        public override async Task Fetch(Uri uri,
                                                 TextWriter writeStream,
                                                 DateTime? lastFetchedDate,
                                                 CancellationToken cancellationToken)
        {
            if (!await HostCacheManager.WaitToFetch(uri.Host, 120000, cancellationToken))
                throw new TimeoutException("Waited too long to start fetching");

            using (var db = new Database())
                await db.SaveWebResource(uri, DateTime.Now.AddDays(7), cancellationToken);

            base.BeginRequest();

            var request = CreateRequest(uri, lastFetchedDate);

            HttpWebResponse response = null;

            try
            {
                var netTask = request.GetResponseAsync();

                await Task.WhenAny(netTask, Task.Delay(request.Timeout, cancellationToken));
                Monitor.Enter(SyncOutput);
                OutputRequest(request, writeStream);
                if (netTask.Status != TaskStatus.RanToCompletion)
                    throw new TimeoutException(string.Format("Request timed out: {0}", request.RequestUri));

                response = await netTask as HttpWebResponse;

                if (IsBlocked(response, out string block_reason))
                {
                    log.Error(string.Format("URI is blocked, {0}. {1}", block_reason, request.RequestUri));
                    response.Close();
                }
                else
                {
                    OutputResponse(response, writeStream);
                }
            }
            catch (Exception ex)
            {
                log.ErrorFormat("HttpResourceFetcher failure: '{0}', {1}", request.RequestUri, ex.Message);
                OutputException(ex, writeStream, request);
            }
            finally
            {
                response?.Dispose();
                response = null;
                base.EndRequest();
                if (Monitor.IsEntered(SyncOutput)) Monitor.Exit(SyncOutput);
            }
        }


        private bool IsBlocked(HttpWebResponse response, out string block_reason)
        {
            block_reason = "OK";
            bool rtn = true;

            if (response.ContentLength > Settings.MaxFileDownloadLengthInBytes)
            {
                block_reason = "Response exceeded max length of " + Settings.MaxFileDownloadLengthInBytes;
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

        HttpWebRequest CreateRequest(Uri uri, DateTime? lastFetchedDate)
        {
            var request = WebRequest.Create(uri) as HttpWebRequest;

            // our user agent
            request.UserAgent = Settings.UserAgent;
            request.Method = "GET";

            // compression yes please!
            request.AutomaticDecompression =
              DecompressionMethods.GZip |
              DecompressionMethods.Deflate;

            // timeout lowered to speed things up
            request.Timeout = 5000;

            // dont want keepalive as we'll be connecting to lots of servers and we're unlikely to get back to this one anytime soon
            request.KeepAlive = false;
            request.Pipelined = false; // similar concept to keepalive

            // dont cache anything this will just waste time
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);

            // will speed up the request if this hasn't changed since last fetch
            if (lastFetchedDate.HasValue)
                request.IfModifiedSince = lastFetchedDate.Value;

            // setup bogus cookies so we get sent something
            // some sites get upset if this isn't here
            request.CookieContainer = new CookieContainer();

            return request;
        }
    }
}
