using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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

        protected static readonly SemaphoreSlim OutputSync = new SemaphoreSlim(1);

        protected static bool OutputInUse { get => OutputSync.CurrentCount == 0; }

        static int _activeFetches;
        public static int ActiveFetches
        {
            get
            {
                return _activeFetches;
            }
            set
            {
                _activeFetches = value;
            }
        }

        protected bool RequestWritten { get; set; }

        public abstract Task Fetch(Uri uri,
                                   XmlWriter writeStream,
                                   IBlockProvider blockProvider,
                                   DateTime? lastFetchedDate);

        public static async Task FetchFactory(Uri uri,
                                    XmlWriter writeStream,
                                    IBlockProvider blockProvider,
                                    DateTime? lastFetchedDate)
        {
            if (!HasHandler(uri))
                log.Error("No handler for URI " + uri);
            else if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                await new HttpResourceFetcher().Fetch(uri, writeStream, blockProvider, lastFetchedDate).ConfigureAwait(false);
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

        protected void OutputStartResource(XmlWriter outStream)
        {
            outStream.WriteStartElement("resource");
        }

        protected void OutputEndResource(XmlWriter outStream)
        {
            outStream.WriteEndElement();
            outStream.Flush();
        }

        protected void OutputRequest(WebRequest request, XmlWriter outstream)
        {
            outstream.WriteStartElement("request");
            outstream.WriteString(string.Format("Uri: {0}\n", request.RequestUri));
            outstream.WriteString(string.Format("ResponseTime: {0}", 0));
            foreach (string key in request.Headers)
            {
                outstream.WriteString(string.Format("{0}: {1}", key, request.Headers[key]));
            }
            outstream.WriteEndElement();
            RequestWritten = true;
        }

        protected void OutputResponse(WebResponse response, XmlWriter outstream)
        {
            outstream.WriteStartElement("response");
            outstream.WriteStartElement("header");

            foreach (string key in response.Headers)
            {
                outstream.WriteString(string.Format("{0}: {1}", key, response.Headers[key]));
            }
            outstream.WriteEndElement(); // header

            try
            {
                outstream.WriteStartElement("data");

                using (var stream = response.GetResponseStream())
                {
                    byte[] buffer = new byte[1024];
                    int l = 0;
                    do
                    {
                        l = stream.Read(buffer, 0, 1024);
                        outstream.WriteBase64(buffer, 0, l);
                    }
                    while (l > 0);
                }

            }
            catch(Exception ex)
            {
                throw;
            }
            finally
            {
                outstream.WriteEndElement(); // data
                outstream.WriteEndElement(); // response
            }
        }

        protected void OutputException(Exception ex, XmlWriter outStream, WebRequest request)
        {
            if (!RequestWritten) OutputRequest(request, outStream);
            outStream.WriteElementString("exception", ex.ToString());
        }
    }
}
