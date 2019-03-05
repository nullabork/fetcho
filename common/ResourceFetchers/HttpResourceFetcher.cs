﻿using System;
using System.Net;
using System.Net.Cache;
using System.Threading;
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

        /// <summary>
        /// Fetches a copy of a HTTP resource
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="writeStream"></param>
        /// <param name="lastFetchedDate">Date we last fetched the resource - helps in optimising resources</param>
        /// <returns></returns>
        public override async Task Fetch(
            Uri uri,
            Uri referrerUri,
            DateTime? lastFetchedDate,
            BufferBlock<WebDataPacketWriter> writerPool
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
            DateTime startTime = DateTime.Now;
            Exception exception = null;
            bool wroteOk = false;

            try
            {
                base.BeginRequest();

                // get database from the pool
                var db = await DatabasePool.GetDatabaseAsync();
                await db.SaveWebResource(uri, DateTime.Now.AddDays(FetchoConfiguration.Current.PageCacheExpiryInDays));
                await DatabasePool.GiveBackToPool(db);

                request = CreateRequest(referrerUri, uri, lastFetchedDate);

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
                    var rspw = WriteOutResponse(writerPool, request, response, startTime);

                    while (rspw.Status != TaskStatus.RanToCompletion && rspw.Status != TaskStatus.Faulted)
                    {
                        if (!firstTime)
                            throw new FetchoException("WriteOutResponse timed out");
                        var wait = Task.Delay(FetchoConfiguration.Current.ResponseReadTimeoutInMilliseconds);
                        await Task.WhenAny(wait, rspw);
                        if (!firstTime && ActiveFetches < 5) log.DebugFormat("Been waiting a while for {0}", request.RequestUri);
                        firstTime = false;
                    }

                    wroteOk = await rspw;
                }
            }
            catch (TimeoutException ex) { ErrorHandler(ex, request, false); exception = ex; }
            catch (AggregateException ex) { ErrorHandler(ex.InnerException, request, false); exception = ex; }
            catch (FetchoResourceBlockedException ex) { ErrorHandler(ex, request, false); exception = ex; }
            catch (WebException ex) { ErrorHandler(ex, request, false); exception = ex; }
            catch (Exception ex) { ErrorHandler(ex, request, true); exception = ex; }
            finally
            {
                try
                {
                    if (!wroteOk)
                    {
                        var packet = await writerPool.ReceiveAsync();
                        packet.OutputStartResource();
                        OutputRequest(request, packet.Writer, startTime);
                        OutputException(exception, packet.Writer);
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
            BufferBlock<WebDataPacketWriter> writers, 
            HttpWebRequest request, 
            HttpWebResponse response, 
            DateTime startTime
            )
        {
            WebDataPacketWriter packet = null;
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
                        l = await readStream.ReadAsync(buffer, bytesread, buffer.Length - bytesread).ConfigureAwait(false);
                        bytesread += l;
                    }
                    while (l > 0 && bytesread < buffer.Length); // read up to the buffer limit and ditch the rest
                }
            }
            catch(Exception ex)
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

                packet.OutputStartResource();
                OutputRequest(request, packet.Writer, startTime);
                if (bytesread > 0)
                    OutputResponse(response, buffer, bytesread, packet.Writer);
            }
            catch (Exception ex) { ErrorHandler(ex, request, false); exception = ex; }
            finally
            {
                OutputException(exception, packet.Writer);
                packet.OutputEndResource();
                await writers.SendAsync(packet);
                wroteOk = true;
            }
            return wroteOk;
        }

        /// <summary>
        /// Create a web request with all the usual suspect configurations
        /// </summary>
        /// <param name="referrerUri"></param>
        /// <param name="uri"></param>
        /// <param name="lastFetchedDate"></param>
        /// <returns></returns>
        private HttpWebRequest CreateRequest(Uri referrerUri, Uri uri, DateTime? lastFetchedDate)
        {
            var request = WebRequest.Create(uri) as HttpWebRequest;

            // our user agent
            request.UserAgent = FetchoConfiguration.Current.UserAgent;
            request.Method = "GET";

            // we need to check the redirect URL is OK from a robots standpoint
            request.AllowAutoRedirect = false; 

            // compression yes please!
            request.AutomaticDecompression =
              DecompressionMethods.GZip |
              DecompressionMethods.Deflate;

            // timeout is not honoured by the framework in async mode, but is implemented manually by this code
            request.Timeout = FetchoConfiguration.Current.DefaultRequestTimeoutInMilliseconds;

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

        private void ErrorHandler(Exception ex, HttpWebRequest request, bool verbose)
        {
            const string format = "'{0}': {1}";

            if (verbose)
                log.ErrorFormat(format, request?.RequestUri, ex);
            else
                log.ErrorFormat(format, request?.RequestUri, ex.Message);

            // In memorandum:
            // The line here was originally if ( !OutputInUse) await OutputSync.WaitAsync();
            // OutputInUse was defined as "OutputSync.CurrentCount == 0"
            // This contains a very subtle race condition it took about 300-400gb of
            // downloading before it finally reared its ugly head and corrupted the 
            // Xml file.
        }
    }
}
