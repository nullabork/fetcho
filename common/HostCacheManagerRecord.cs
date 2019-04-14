using System;
using System.Threading;


namespace Fetcho.Common
{
    /// <summary>
    /// Used by HostCacheManager to store cache information
    /// </summary>
    internal class HostCacheManagerRecord : IDisposable
    {
        /// <summary>
        /// Host in question
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Last time this host was requested
        /// </summary>
        public DateTime LastCall { get; set; }
        public int TouchCount { get; set; }
        public int MaxFetchSpeedInMilliseconds { get; set; }
        public SemaphoreSlim UpdateWaitHandle { get; set; }
        public SemaphoreSlim FetchWaitHandle { get; set; }
        public RobotsFile Robots { get; set; }
        public bool UpdateRobotsFileRequired { get { return !RobotsChecked; } }
        public bool RobotsChecked { get; set; }

        /// <summary>
        /// Keeps a count of network issues such as timeouts, failures to connect
        /// </summary>
        public int NetworkIssues { get; set; }

        /// <summary>
        /// True if this host uses compression
        /// </summary>
        public bool UsesCompression { get; set; }

        /// <summary>
        /// True if this supports SSL
        /// </summary>
        public bool SupportsSsl { get; set; }

        /// <summary>
        /// Returns true if the host has not been accessed recently
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public bool IsFetchable { get { return LastCall.AddMilliseconds(MaxFetchSpeedInMilliseconds) < DateTime.UtcNow; } }

        public HostCacheManagerRecord()
        {
            MaxFetchSpeedInMilliseconds = FetchoConfiguration.Current.MaxFetchSpeedInMilliseconds;
            LastCall = DateTime.MinValue;
            UpdateWaitHandle = new SemaphoreSlim(1);
            FetchWaitHandle = new SemaphoreSlim(1);
            RobotsChecked = false;
        }

        public void SetFromHostInfo(HostInfo info)
        {
            SupportsSsl = info.SupportsSsl;
            UsesCompression = info.UsesCompression;
            MaxFetchSpeedInMilliseconds = info.MaxFetchSpeedInMilliseconds;
        }

        public HostInfo GetHostInfo()
            => new HostInfo()
            {
                Host = Host,
                SupportsSsl = SupportsSsl,
                UsesCompression = UsesCompression,
                MaxFetchSpeedInMilliseconds = MaxFetchSpeedInMilliseconds
            };

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Robots?.Dispose();
                    UpdateWaitHandle?.Dispose(); // we're disposing whilst people have this accessed. Do we lock here ?
                    FetchWaitHandle?.Dispose();// we're disposing whilst people have this accessed. Do we lock here ? 
                    UpdateWaitHandle = null;
                    FetchWaitHandle = null;
                }

                Robots = null;
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion


    }

    public struct HostInfo
    {
        public string Host; 
        public bool SupportsSsl;
        public bool UsesCompression;
        public int MaxFetchSpeedInMilliseconds;

        #region Equals and GetHashCode implementation
        public override bool Equals(object obj)
        {
            if (!(obj is HostInfo)) return false;
            return object.Equals(this.Host, ((HostInfo)obj).Host);
        }

        public override int GetHashCode()
        {
            int hashCode = 0;
            unchecked
            {
                if (Host != null)
                    hashCode += 1000000007 * Host.GetHashCode();
            }
            return hashCode;
        }

        public static bool operator ==(HostInfo lhs, HostInfo rhs)
        {
            if (ReferenceEquals(lhs, rhs))
                return true;
            if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null))
                return false;
            return lhs.Equals(rhs);
        }

        public static bool operator !=(HostInfo lhs, HostInfo rhs)
        {
            return !(lhs == rhs);
        }

        #endregion

        public readonly static HostInfo None = new HostInfo();
    }
}
