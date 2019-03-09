using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;
using log4net;

namespace Fetcho.Common
{
    /// <summary>
    /// Generalised class for fetching resources from the internet - abstracts away the individual protocol specifics
    /// </summary>
    public abstract class ResourceFetcher
    {
        static readonly ILog log = LogManager.GetLogger(typeof(ResourceFetcher));

        /// <summary>
        /// Number of active downloads
        /// </summary>
        public static int ActiveFetches { get => _activeFetches; set => _activeFetches = value; }
        static int _activeFetches;

        /// <summary>
        /// Number of 'threads' waiting to write to the file
        /// </summary>
        public static int WaitingToWrite { get => _waitingToWrite; set => _waitingToWrite = value; }
        static int _waitingToWrite;

        public abstract Task Fetch(
            Uri uri,
            Uri referrerUri,
            DateTime? lastFetchedDate,
            BufferBlock<WebDataPacketWriter> writerPool
            );

        /// <summary>
        /// For any URI fires up a fetcher to get it
        /// </summary>
        /// <param name="referrerUri"></param>
        /// <param name="uri"></param>
        /// <param name="writeStream"></param>
        /// <param name="blockProvider"></param>
        /// <param name="lastFetchedDate"></param>
        /// <returns></returns>
        public static async Task FetchFactory(
            Uri uri,
            Uri referrerUri,
            DateTime? lastFetchedDate,
            BufferBlock<WebDataPacketWriter> writerPool
            )
        {
            if (!HasHandler(uri))
                log.Error("No handler for URI " + uri);
            else if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                await new HttpResourceFetcher().Fetch(
                    uri,
                    referrerUri,
                    lastFetchedDate,
                    writerPool).ConfigureAwait(false);
            //      else if ( uri.Scheme == Uri.UriSchemeFtp )
            //        new FtpResourceFetcher().Fetch(uri, writeStream, lastFetchedDate);
        }

        /// <summary>
        /// Returns true if the URI can be handled by the factory method
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static bool HasHandler(Uri uri) => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

        protected void BeginRequest() => Interlocked.Increment(ref _activeFetches);

        protected void EndRequest() => Interlocked.Decrement(ref _activeFetches);

        protected void OutputRequest(WebRequest request, XmlWriter outstream, DateTime startTime)
        {
            if (request == null)
            {
                outstream.WriteStartElement("request");
                outstream.WriteEndElement();
            }
            else
            {
                DateTime now = DateTime.Now;
                outstream.WriteStartElement("request");
                outstream.WriteString(string.Format("Uri: {0}\n", request.RequestUri == null ? "" : request.RequestUri.ToString().CleanupForXml()));
                outstream.WriteString(string.Format("ResponseTime: {0}\n", now - startTime));
                outstream.WriteString(string.Format("Date: {0}\n", now));
                // AllKeys is slower than Keys but is a COPY to prevent errors from updates to the collection
                foreach (string key in request.Headers.AllKeys)
                {
                    outstream.WriteString(string.Format("{0}: {1}\n", key, request.Headers[key].CleanupForXml()));
                }
                outstream.WriteEndElement();
            }
        }

        protected void OutputResponse(WebResponse response, byte[] buffer, int bytesRead, XmlWriter outStream)
        {
            outStream.WriteStartElement("response");
            outStream.WriteStartElement("header");

            if ( response is HttpWebResponse httpWebResponse)
            {
                outStream.WriteString(string.Format("status: {0} {1}\n", httpWebResponse.StatusCode, httpWebResponse.StatusDescription));
            }

            foreach (string key in response.Headers)
            {
                outStream.WriteString(string.Format("{0}: {1}\n", key, response.Headers[key]));
            }
            outStream.WriteEndElement(); // header

            try
            {
                outStream.WriteStartElement("data");
                outStream.WriteBase64(buffer, 0, bytesRead);

            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                outStream.WriteEndElement(); // data
                outStream.WriteEndElement(); // response
                response.Close();
            }
        }

        protected void OutputException(Exception ex, XmlWriter outStream)
        {
            if (ex == null) return;
            outStream.WriteElementString("exception", ex.ToString().CleanupForXml());
        }

        protected void IncWaitingToWrite() => Interlocked.Increment(ref _waitingToWrite);
        protected void DecWaitingToWrite() => Interlocked.Decrement(ref _waitingToWrite);

    }
}
