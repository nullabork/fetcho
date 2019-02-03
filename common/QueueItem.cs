using System;

namespace Fetcho.Common
{
    public class QueueItem
    {
        public const uint BadQueueItemSequenceNumber = uint.MaxValue;

        public uint Sequence
        {
            get;
            set;
        }

        public Uri TargetUri
        {
            get;
            set;
        }

        public Uri SourceUri
        {
            get;
            set;
        }

        public bool HasAnIssue
        {
            get => Sequence == BadQueueItemSequenceNumber || BlockedByRobots || MalformedUrl || SequenceTooHigh || UnsupportedUri || IsProbablyBlocked;
        }

        public bool BlockedByRobots { get; set; }

        public bool UnsupportedUri { get; set; }

        public bool MalformedUrl { get; set; }

        public bool SequenceTooHigh { get; set; }

        public bool IsProbablyBlocked { get; set; }

        public string StateCode
        {
            get
            {

                string code = "";

                if (Sequence == BadQueueItemSequenceNumber) code += 'B';
                if (MalformedUrl) code += 'M';
                if (SequenceTooHigh) code += 'H';
                if (BlockedByRobots) code += 'R';
                if (UnsupportedUri) code += 'U';
                if (IsProbablyBlocked) code += 'P';

                return code;
            }
        }

        public QueueItem()
        {
            Sequence = QueueItem.BadQueueItemSequenceNumber;
        }

        public override string ToString()
        {
            try
            {
                return String.Format("{0}\t{1}\t{2}\t{3}", StateCode, Sequence, SourceUri, TargetUri);
            }
            catch(Exception)
            {
                return "B";
            }
        }

        public static QueueItem Parse(string line)
        {
            try
            {
                string[] tokens = line.Split('\t');

                if (tokens.Length < 4) return null;


                QueueItem item = new QueueItem()
                {
                    Sequence = uint.Parse(tokens[1]),
                    SourceUri = new Uri(tokens[2]),
                    TargetUri = new Uri(tokens[3])
                };

                item.BlockedByRobots = tokens[0].Contains("R");
                item.SequenceTooHigh = tokens[0].Contains("H");
                item.MalformedUrl = tokens[0].Contains("M");
                item.UnsupportedUri = tokens[0].Contains("U");
                item.IsProbablyBlocked = tokens[0].Contains("P");

                if (item.TargetUri == null)
                    return null;
                return item;
            }
            catch( Exception ex )
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



