using System;
using System.Net;
using System.Net.Cache;
using System.Threading;

using System.Threading.Tasks;
using System.Xml;
using log4net;

namespace Fetcho.Common
{
    /// <summary>
    /// Fetches HTTP resources from the web
    /// </summary>
    public class HttpResourceFetcher : ResourceFetcher
    {
        static readonly ILog log = LogManager.GetLogger(typeof(HttpResourceFetcher));

        /// <summary>
        /// Fetches a copy of a HTTP resource
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="writeStream"></param>
        /// <param name="lastFetchedDate">Date we last fetched the resource - helps in optimising resources</param>
        /// <returns></returns>
        public override async Task Fetch(Uri uri,
                                                 XmlWriter writeStream,
                                                 IBlockProvider blockProvider,
                                                 DateTime? lastFetchedDate)
        {
            using (var db = new Database())
                await db.SaveWebResource(uri, DateTime.Now.AddDays(7));

            base.BeginRequest();

            var request = CreateRequest(uri, lastFetchedDate);

            HttpWebResponse response = null;

            try
            {
                var netTask = request.GetResponseAsync();

                await Task.WhenAny(netTask, Task.Delay(request.Timeout)).ConfigureAwait(false);
                await OutputSync.WaitAsync();
                OutputStartResource(writeStream);
                OutputRequest(request, writeStream);
                if (netTask.Status != TaskStatus.RanToCompletion)
                {
                    if (netTask.Exception != null)
                        throw netTask.Exception;
                    else
                        throw new TimeoutException(string.Format("Request timed out: {0}", request.RequestUri));
                }

                response = await netTask as HttpWebResponse;

                if (blockProvider.IsBlocked(request, response, out string block_reason))
                {
                    response.Close();
                    throw new Exception(string.Format("URI is blocked, {0}", block_reason));
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
                OutputEndResource(writeStream);
                base.EndRequest();
                if ( OutputInUse ) OutputSync.Release();
            }
        }

        private HttpWebRequest CreateRequest(Uri uri, DateTime? lastFetchedDate)
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
            request.Timeout = 10000;

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
