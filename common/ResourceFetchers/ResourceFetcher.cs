using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using log4net;

namespace Fetcho.Common
{
    /// <summary>
    /// Generalised class for fetching resources from the internet - abstracts away the individual protocol specifics
    /// </summary>
    public abstract class ResourceFetcher
    {
        static readonly ILog log = LogManager.GetLogger(typeof(ResourceFetcher));

        public static readonly object SyncOutput = new object();

        public const int NothingActive = 0;
        public const string StartOfRequestSection = "\n";
        public const string EndOfRequestSection = "\n";
        public const string StartOfResponseSection = "RESPONSE";
        public const string EndOfResponseSection = "\n\0";
        public const string StartOfExceptionSection = "EXCEPTION";
        public const string EndOfExceptionSection = "\n\0";

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
                                   TextWriter writeStream,
                                   DateTime? lastFetchedDate,
                                   CancellationToken cancellationToken);

        public static async Task FetchFactory(Uri uri,
                                    TextWriter writeStream,
                                    DateTime? lastFetchedDate,
                                    CancellationToken cancellationToken)
        {
            if (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps)
                await new HttpResourceFetcher().Fetch(uri, writeStream, lastFetchedDate, cancellationToken);
            //      else if ( uri.Scheme == Uri.UriSchemeFtp )
            //        new FtpResourceFetcher().Fetch(uri, writeStream, lastFetchedDate);
            else
                log.Error("No handler for URI " + uri);
        }

        protected void BeginRequest()
        {
            Interlocked.Increment(ref _activeFetches);

        }

        protected void EndRequest()
        {
            Interlocked.Decrement(ref _activeFetches);

        }

        protected void OutputRequest(WebRequest request, TextWriter outstream)
        {
            outstream.Write(StartOfRequestSection);
            outstream.WriteLine("Uri: {0}", request.RequestUri);
            outstream.WriteLine("ResponseTime: {0}", 0);
            foreach (string key in request.Headers)
            {
                outstream.WriteLine("{0}: {1}", key, request.Headers[key]);
            }
            outstream.WriteLine(EndOfRequestSection);
            RequestWritten = true;
        }

        protected void OutputResponse(WebResponse response, TextWriter outstream)
        {
            outstream.WriteLine(StartOfResponseSection);

            foreach (string key in response.Headers)
            {
                outstream.WriteLine("{0}: {1}", key, response.Headers[key]);
            }

            outstream.WriteLine();
            outstream.WriteLine();

            using (var stream = new StreamReader(response.GetResponseStream()))
            {
                while (!stream.EndOfStream)
                {
                    outstream.WriteLine(stream.ReadLine());
                }
            }
        }

        protected void OutputException(Exception ex, TextWriter outStream, WebRequest request)
        {
            if (!RequestWritten) OutputRequest(request, outStream);
            outStream.WriteLine(StartOfExceptionSection);
            outStream.WriteLine(ex);
            outStream.WriteLine(EndOfExceptionSection);
        }
    }
}
