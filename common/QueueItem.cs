using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Fetcho.Common
{
    /// <summary>
    /// A link that is on the queue for crawling
    /// </summary>
    public class QueueItem
    {
        /// <summary>
        /// Represents a queue item that should not be fetched
        /// </summary>
        public const uint BadQueueItemPriorityNumber = uint.MaxValue;

        /// <summary>
        /// Will report the status of this queue item as it moves through fetcho
        /// </summary>
        public EventHandler<QueueItemStatusUpdateEventArgs> StatusUpdate;
        private readonly object statusUpdateLock = new object();

        /// <summary>
        /// Use this to send status updates
        /// </summary>
        /// <param name="status"></param>
        protected void OnStatusUpdate()
        {
            if (StatusUpdate == null) return;

            var e = new QueueItemStatusUpdateEventArgs
            {
                Item = this
            };

            lock (statusUpdateLock)
            {
                e.Status = Status;
                StatusUpdate?.Invoke(this, e);
            }
        }

        public QueueItemStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnStatusUpdate();
            }
        }
        private QueueItemStatus _status = QueueItemStatus.Unknown;

        /// <summary>
        /// True if this queue item can be discarded for any reason
        /// </summary>
        public bool CanBeDiscarded { get; set; }

        /// <summary>
        /// Priority to go fetch the link, lower is better
        /// </summary>
        public uint Priority { get; set; }

        /// <summary>
        /// Where does the link point to
        /// </summary>
        public Uri TargetUri { get; set; }

        /// <summary>
        /// IPAddress of the target
        /// </summary>
        public IPAddress TargetIP { get; set; }

        /// <summary>
        /// What page pointed from it?
        /// </summary>
        public Uri SourceUri { get; set; }

        /// <summary>
        /// True if this item has an issue
        /// </summary>
        public bool HasAnIssue
        {
            get => Priority == BadQueueItemPriorityNumber ||
                BlockedByRobots ||
                MalformedUrl ||
                PriorityTooLow ||
                UnsupportedUri ||
                IsProbablyBlocked ||
                ChunkSequenceTooHigh ||
                IPSeenRecently ||
                IsBlockedByDomain;
        }

        /// <summary>
        /// This item is blocked by a robots rule
        /// </summary>
        public bool BlockedByRobots { get; set; }

        /// <summary>
        /// The URI is unsupported by the fetcho software eg. android-app://
        /// </summary>
        public bool UnsupportedUri { get; set; }

        /// <summary>
        /// The URL is malformed for some reason
        /// </summary>
        public bool MalformedUrl { get; set; }

        /// <summary>
        /// The priority is not high enough to fetch
        /// </summary>
        public bool PriorityTooLow { get; set; }

        /// <summary>
        /// A cheap guess is this will be blocked by more expensive testing
        /// </summary>
        public bool IsProbablyBlocked { get; set; }

        /// <summary>
        /// </summary>
        public bool VisitedRecently { get; set; }

        /// <summary>
        /// This item is blocked by a domain name block
        /// </summary>
        public bool IsBlockedByDomain { get; set; }

        /// <summary>
        /// </summary>
        public bool ChunkSequenceTooHigh { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IPSeenRecently { get; set; }

        /// <summary>
        /// Chunk sequence
        /// </summary>
        public int ChunkSequence { get; set; }

        /// <summary>
        /// Number of milliseconds to timeout
        /// </summary>
        public int ReadTimeoutInMilliseconds { get; set; }

        /// <summary>
        /// Combined statecode for the flags
        /// </summary>
        public string StateCode
        {
            get
            {

                string code = "";

                if (Priority == BadQueueItemPriorityNumber) code += 'B';
                if (MalformedUrl) code += 'M';
                if (PriorityTooLow) code += 'H';
                if (BlockedByRobots) code += 'R';
                if (UnsupportedUri) code += 'U';
                if (IsProbablyBlocked) code += 'P';
                if (IsBlockedByDomain) code += 'L';
                if (VisitedRecently) code += 'V';
                if (IPSeenRecently) code += 'I';
                if (ChunkSequenceTooHigh) code += 'C';

                return code;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) return;
                BlockedByRobots = value.Contains("R");
                PriorityTooLow = value.Contains("H");
                MalformedUrl = value.Contains("M");
                UnsupportedUri = value.Contains("U");
                IsProbablyBlocked = value.Contains("P");
                IsBlockedByDomain = value.Contains("L");
                VisitedRecently = value.Contains("V");
                IPSeenRecently = value.Contains("I");
                ChunkSequenceTooHigh = value.Contains("C");
            }
        }

        public QueueItem()
        {
            Priority = QueueItem.BadQueueItemPriorityNumber;
            TargetIP = IPAddress.None;
            CanBeDiscarded = true;
            ReadTimeoutInMilliseconds = FetchoConfiguration.Current.ResponseReadTimeoutInMilliseconds;
        }

        public override string ToString()
        {
            try
            {
                return String.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", StateCode, Priority, TargetUri, SourceUri, TargetIP, ChunkSequence);
            }
            catch(Exception)
            {
                return "B";
            }
        }

        /// <summary>
        /// Create a queue item from a tab seperated line - use ToString() to make an appropriate line
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static QueueItem Parse(string line)
        {
            IPAddress targetAddr = IPAddress.None;
            int chunkSequence = 0;

            try
            {
                string[] tokens = line.Split('\t');

                if (tokens.Length < 5) return null;
                if ( tokens.Length >= 5 ) IPAddress.TryParse(tokens[4], out targetAddr);
                if ( tokens.Length >= 6 ) int.TryParse(tokens[5], out chunkSequence);

                QueueItem item = new QueueItem()
                {
                    StateCode = tokens[0],
                    Priority = uint.Parse(tokens[1]),
                    TargetUri = new Uri(tokens[2]),
                    SourceUri = new Uri(tokens[3]),
                    TargetIP = targetAddr,
                    ChunkSequence = chunkSequence
                };

                if (item.TargetUri == null)
                    return null;
                return item;
            }
            catch( Exception )
            {
                return null;
            }
        }

        #region Equals and GetHashCode implementation
        public override bool Equals(object obj)
        {
            QueueItem other = obj as QueueItem;
            if (other == null)
                return false;
            return object.Equals(this.TargetUri, other.TargetUri);
        }

        public override int GetHashCode()
        {
            int hashCode = 0;
            unchecked
            {
                if (TargetUri != null)
                    hashCode += 1000000007 * TargetUri.GetHashCode();
            }
            return hashCode;
        }

        public static bool operator ==(QueueItem lhs, QueueItem rhs)
        {
            if (ReferenceEquals(lhs, rhs))
                return true;
            if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null))
                return false;
            return lhs.Equals(rhs);
        }

        public static bool operator !=(QueueItem lhs, QueueItem rhs)
        {
            return !(lhs == rhs);
        }

        #endregion

    }
}



