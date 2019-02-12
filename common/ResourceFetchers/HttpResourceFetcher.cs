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
        public override async Task Fetch(
            Uri referrerUri,
            Uri uri,
            XmlWriter writeStream,
            IBlockProvider blockProvider,
            DateTime? lastFetchedDate)
        {
            using (var db = new Database())
                await db.SaveWebResource(uri, DateTime.Now.AddDays(7));

            base.BeginRequest();

            var request = CreateRequest(referrerUri, uri, lastFetchedDate);

            HttpWebResponse response = null;
            DateTime startTime = DateTime.Now;
            Exception exception = null;
            bool wroteOk = false;

            try
            {
                var netTask = request.GetResponseAsync();

                await Task.WhenAny(netTask, Task.Delay(request.Timeout)).ConfigureAwait(false);
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
                    throw new FetchoResourceBlockedException(block_reason);
                }
                else
                {
                    await OutputSync.WaitAsync();
                    try
                    {
                        OutputStartResource(writeStream);
                        OutputRequest(request, writeStream, startTime);
                        OutputResponse(response, writeStream);
                    }
                    catch (Exception ex) { ErrorHandler(ex, writeStream, request, false, startTime); exception = ex; }
                    finally
                    {
                        OutputException(exception, writeStream, request, startTime);
                        OutputEndResource(writeStream);
                        base.EndRequest();
                        OutputSync.Release();
                        wroteOk = true;
                    }
                }
            }
            catch (TimeoutException ex) { ErrorHandler(ex, writeStream, request, false, startTime); exception = ex; }
            catch (AggregateException ex) { ErrorHandler(ex.InnerException, writeStream, request, false, startTime); exception = ex; }
            catch (FetchoResourceBlockedException ex) { ErrorHandler(ex, writeStream, request, false, startTime); exception = ex; }
            catch (WebException ex) { ErrorHandler(ex, writeStream, request, false, startTime); exception = ex; }
            catch (InvalidOperationException ex)
            {
                log.ErrorFormat("Barfing because of a corrupt XML: {0}", ex);
                log.InfoFormat("WriteState: {0}", writeStream.WriteState);
                Environment.Exit(1);
            }
            catch (Exception ex) { ErrorHandler(ex, writeStream, request, true, startTime); exception = ex; }
            finally
            {
                //try
                //{
                //    if (!wroteOk)
                //    {
                //        await OutputSync.WaitAsync();
                //        OutputStartResource(writeStream);
                //        OutputRequest(request, writeStream, startTime);
                //        OutputException(exception, writeStream, request, startTime);
                //        OutputEndResource(writeStream);
                //        base.EndRequest();
                //        OutputSync.Release();
                //    }
                //}
                //catch( Exception ex)
                //{
                //    log.Error(ex);
                //}
                response?.Dispose();
                response = null;
            }
        }

        private HttpWebRequest CreateRequest(Uri referrerUri, Uri uri, DateTime? lastFetchedDate)
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
            request.Timeout = 30000;

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

            // fill in the referrer if its set
            TrySetReferrer(request, referrerUri);

            return request;
        }

        private void TrySetReferrer(HttpWebRequest request, Uri uri)
        {
            try
            {
                if (uri != null)
                    request.Referer = uri.ToString();
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to set Referrer: {0}, {1}", uri, ex);
            }
        }

        private void ErrorHandler(Exception ex, XmlWriter writer, HttpWebRequest request, bool verbose, DateTime startTime)
        {
            const string format = "'{0}': {1}";

            if (verbose)
                log.ErrorFormat(format, request.RequestUri, ex);
            else
                log.ErrorFormat(format, request.RequestUri, ex.Message);

            // In memorandum:
            // The line here was originally if ( !OutputInUse) await OutputSync.WaitAsync();
            // OutputInUse was defined as "OutputSync.CurrentCount == 0"
            // This contains a very subtle race condition it took about 300-400gb of
            // downloading before it finally reared its ugly head and corrupted the 
            // Xml file.
        }
    }
}
