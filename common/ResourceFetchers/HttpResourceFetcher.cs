using System;
using System.Net;
using System.Net.Cache;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using log4net;

namespace Fetcho.Common
{
    /// <summary>
    /// Fetches HTTP resources from the web
    /// </summary>
    public class HttpResourceFetcher : ResourceFetcher
    {
        static readonly ILog log = LogManager.GetLogger(typeof(HttpResourceFetcher));

        public HttpResourceFetcher()
        {
            if (FetchoConfiguration.Current.BlockProvider == null)
                FetchoConfiguration.Current.BlockProvider = new NullBlockProvider();
        }

        /// <summary>
        /// Fetches a copy of a HTTP resource
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="writeStream"></param>
        /// <param name="lastFetchedDate">Date we last fetched the resource - helps in optimising resources</param>
        /// <returns></returns>
        public override async Task Fetch(
            QueueItem queueItem,
            Uri uri,
            Uri refererUri,
            DateTime? lastFetchedDate,
            BufferBlock<IWebResourceWriter> writerPool
            )
        {

            // the process is 
            // 1. update the DB to say we're fetching
            // 2. create a request
            // 3. wait for the response
            // 4. download the response into memory buffers
            // 5. aquire a write lock
            // 6. write to the XML
            // 7. clean up

            HttpWebResponse response = null;
            HttpWebRequest request = null;
            DateTime startTime = DateTime.UtcNow;
            Exception exception = null;
            bool wroteOk = false;

            try
            {
                base.BeginRequest();

                request = CreateRequest(queueItem, refererUri, uri, lastFetchedDate);

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

                if (FetchoConfiguration.Current.BlockProvider.IsBlocked(request, response, out string block_reason))
                {
                    response.Close();
                    throw new FetchoResourceBlockedException(block_reason);
                }
                else
                {
                    bool firstTime = true;
                    var rspw = WriteOutResponse(queueItem, writerPool, request, response, startTime);

                    while (rspw.Status != TaskStatus.RanToCompletion && rspw.Status != TaskStatus.Faulted)
                    {
                        if (!firstTime)
                            throw new TimeoutException("WriteOutResponse timed out");
                        var wait = Task.Delay(queueItem == null ? FetchoConfiguration.Current.ResponseReadTimeoutInMilliseconds : queueItem.ReadTimeoutInMilliseconds);
                        await Task.WhenAny(wait, rspw);
                        if (!firstTime && ActiveFetches < 5) log.DebugFormat("Been waiting a while for {0}", request.RequestUri);
                        firstTime = false;
                    }

                    wroteOk = await rspw;

                    //await DoBookkeeping(queueItem, request);
                }
            }
            catch (Exception ex)
            {
                await ErrorHandler(ex, request, queueItem);
                exception = ex;
            }
            finally
            {
                try
                {
                    if (!wroteOk)
                    {
                        var packet = await writerPool.ReceiveAsync();
                        packet.OutputStartResource(queueItem);
                        packet.OutputRequest(request, startTime);
                        packet.OutputException(exception);
                        packet.OutputEndResource();
                        await writerPool.SendAsync(packet);
                    }
                }
                catch (Exception ex)
                {
                    log.ErrorFormat("Barfing because we got an error in the error handling code: {0}", ex);
                    Environment.Exit(1);
                }
                response?.Dispose();
                response = null;
                base.EndRequest();
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="writers"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="startTime"></param>
        /// <returns></returns>
        private async Task<bool> WriteOutResponse(
            QueueItem queueItem,
            BufferBlock<IWebResourceWriter> writers,
            HttpWebRequest request,
            HttpWebResponse response,
            DateTime startTime
            )
        {
            IWebResourceWriter packet = null;
            Exception exception = null;
            bool wroteOk = false;

            // this has a potential to cause memory issues if theres lots of waiting
            byte[] buffer = new byte[FetchoConfiguration.Current.MaxFileDownloadLengthInBytes];
            int bytesread = 0;

            // Read as much into memory as possible up to the max limit
            try
            {
                using (var readStream = response.GetResponseStream())
                {
                    int l = 0;
                    do
                    {
                        l = await readStream.ReadAsync(buffer, bytesread, buffer.Length - bytesread); // dont configureawait - disposed?
                        bytesread += l;
                    }
                    while (l > 0 && bytesread < buffer.Length); // read up to the buffer limit and ditch the rest
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                exception = ex;
                bytesread = 0;
            }

            try
            {
                // once we've got plenty of bytes go find a lock
                IncWaitingToWrite();
                packet = await writers.ReceiveAsync().ConfigureAwait(false);
                DecWaitingToWrite();

                packet.OutputStartResource(queueItem);
                packet.OutputRequest(request, startTime);
                if (bytesread > 0)
                    packet.OutputResponse(response, buffer, bytesread);
            }
            catch (Exception ex)
            {
                await ErrorHandler(ex, request, queueItem);
                exception = ex;
            }
            finally
            {
                packet.OutputException(exception);
                packet.OutputEndResource();
                await writers.SendAsync(packet).ConfigureAwait(false);
                wroteOk = true;
            }
            return wroteOk;
        }

        /// <summary>
        /// Create a web request with all the usual suspect configurations
        /// </summary>
        /// <param name="refererUri"></param>
        /// <param name="uri"></param>
        /// <param name="lastFetchedDate"></param>
        /// <returns></returns>
        private HttpWebRequest CreateRequest(QueueItem queueItem, Uri refererUri, Uri uri, DateTime? lastFetchedDate)
        {
            var request = WebRequest.Create(uri) as HttpWebRequest;

            // our user agent
            request.UserAgent = FetchoConfiguration.Current.UserAgent;
            request.Method = "GET";

            // we'll accept anything
            //request.Headers.Set(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            // we need to check the redirect URL is OK from a robots standpoint
            if (queueItem != null)
                request.AllowAutoRedirect = !queueItem.CanBeDiscarded;
            else
                request.AllowAutoRedirect = false;

            // compression yes please!
            request.AutomaticDecompression =
              DecompressionMethods.GZip |
              DecompressionMethods.Deflate;

            // timeout is not honoured by the framework in async mode, but is implemented manually by this code
            request.Timeout = FetchoConfiguration.Current.RequestTimeoutInMilliseconds;

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

            // fill in the referer if its set
            TrySetReferer(request, refererUri);

            return request;
        }

        /// <summary>
        /// Sometimes this doesn't work for whatever reason
        /// </summary>
        /// <param name="request"></param>
        /// <param name="uri"></param>
        private void TrySetReferer(HttpWebRequest request, Uri uri)
        {
            try
            {
                if (uri != null)
                    request.Referer = uri.ToString();
            }
            catch (ArgumentException)
            {
                //log.ErrorFormat("Failed to set Referer: {0}, {1}", uri, ex);
            }
        }

        private async Task RecordNetworkIssues(IPAddress ipAddress)
        {
            if (!IPAddress.None.Equals(ipAddress))
                await FetchoConfiguration.Current.HostCache.RecordNetworkIssue(ipAddress);
        }

        private async Task RecordNetworkIssues(QueueItem item)
        {
            if (item != null && item.TargetIP != null)
                await RecordNetworkIssues(item.TargetIP);
        }

        private async Task IncreaseFetchTimeoutForHost(IPAddress ipAddress)
        {
            var host = await FetchoConfiguration.Current.HostCache.GetHostInfo(ipAddress);
            host.MaxFetchSpeedInMilliseconds += 5000;
            await FetchoConfiguration.Current.HostCache.UpdateHostSettings(host);
        }

        private async Task ErrorHandler(Exception ex, HttpWebRequest request, QueueItem queueItem)
        {
            IncFetchExceptions();

            if (ex is AggregateException aggex)
                ex = aggex.InnerException;

            if ( ex is FetchoResourceBlockedException)
            {
                // do nothing ignore it
            }
            else if (ex is WebException webex)
            {
                if (webex.InnerException is SocketException)
                {
                    if (queueItem == null) await RecordNetworkIssues(await Utility.GetHostIPAddress(request?.RequestUri));
                    else await RecordNetworkIssues(queueItem);
                    TerseExceptionOutput(request?.RequestUri, ex);
                }
                else if (webex.Response is HttpWebResponse resp)
                {
                    if (resp.StatusCode == (HttpStatusCode)429) // too fast - increase our wait time
                    {
                        if (queueItem == null) await IncreaseFetchTimeoutForHost(await Utility.GetHostIPAddress(request?.RequestUri));
                        else await IncreaseFetchTimeoutForHost(queueItem.TargetIP);
                        TerseExceptionOutput(request?.RequestUri, ex);
                    }
                    else if ( resp.StatusCode == HttpStatusCode.NotFound)
                    {
                        IncNotFound();
                    }
                    else if ( resp.StatusCode == HttpStatusCode.Forbidden || resp.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        IncForbidden();
                    }
                    else
                        TerseExceptionOutput(request?.RequestUri, ex);
                }
            }
            else if (ex is TimeoutException timeout)
            {
                if (queueItem == null) await RecordNetworkIssues(await Utility.GetHostIPAddress(request?.RequestUri));
                else await RecordNetworkIssues(queueItem);
                //TerseExceptionOutput(request?.RequestUri, ex);
            }
            else
            {
                VerboseExceptionOutput(request?.RequestUri, ex);
            }

            // In memorandum:
            // The line here was originally if ( !OutputInUse) await OutputSync.WaitAsync();
            // OutputInUse was defined as "OutputSync.CurrentCount == 0"
            // This contains a very subtle race condition it took about 300-400gb of
            // downloading before it finally reared its ugly head and corrupted the 
            // Xml file.
        }

        const string ExceptionMessageFormat = "'{0}': {1}";

        private void TerseExceptionOutput(Uri uri, Exception ex)
            => log.ErrorFormat(ExceptionMessageFormat, uri, ex.Message);

        private void VerboseExceptionOutput(Uri uri, Exception ex)
            => log.ErrorFormat(ExceptionMessageFormat, uri, ex.ToString());
    }
}
