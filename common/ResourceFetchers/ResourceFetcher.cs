using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
        public static int ActiveFetches { get => _activeFetches; protected set => _activeFetches = value; }
        static int _activeFetches;

        /// <summary>
        /// Number of 'threads' waiting to write to the file
        /// </summary>
        public static int WaitingToWrite { get => _waitingToWrite; protected set => _waitingToWrite = value; }
        static int _waitingToWrite;

        public static int FetchExceptions { get => _fetchExceptions; protected set => _fetchExceptions = value; }
        static int _fetchExceptions;

        public static int ForbiddenExceptions { get => _forbiddenExceptions; protected set => _forbiddenExceptions = value; }
        static int _forbiddenExceptions;

        public static int NotFoundExceptions { get => _notFoundExceptions; protected set => _notFoundExceptions = value; }
        static int _notFoundExceptions;

        public abstract Task Fetch(
            QueueItem queueItem,
            Uri uri,
            Uri refererUri,
            DateTime? lastFetchedDate,
            BufferBlock<IWebResourceWriter> writerPool
            );

        /// <summary>
        /// For any URI fires up a fetcher to get it
        /// </summary>
        /// <param name="refererUri"></param>
        /// <param name="uri"></param>
        /// <param name="writeStream"></param>
        /// <param name="blockProvider"></param>
        /// <param name="lastFetchedDate"></param>
        /// <returns></returns>
        public static async Task FetchFactory(
            QueueItem queueItem,
            Uri uri,
            Uri refererUri,
            DateTime? lastFetchedDate,
            BufferBlock<IWebResourceWriter> writerPool
            )
        {
            if (!HasHandler(uri))
                log.Error("No handler for URI " + uri);
            else if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                await new HttpResourceFetcher().Fetch(
                    queueItem,
                    uri,
                    refererUri,
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


        protected void IncFetchExceptions() => Interlocked.Increment(ref _fetchExceptions);
        protected void IncForbidden() => Interlocked.Increment(ref _forbiddenExceptions);
        protected void IncNotFound() => Interlocked.Increment(ref _notFoundExceptions);
        protected void IncWaitingToWrite() => Interlocked.Increment(ref _waitingToWrite);
        protected void DecWaitingToWrite() => Interlocked.Decrement(ref _waitingToWrite);

    }
}
