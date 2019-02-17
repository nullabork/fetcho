
using System;

namespace Fetcho.Common.Entities
{
    public class Site
    {
        public MD5Hash Hash { get { return MD5Hash.Compute(HostName); } }

        public string HostName { get; set; }

        public bool IsBlocked { get; set; }

        public DateTime? LastRobotsFetched { get; set; }

        public RobotsFile RobotsFile { get; set; }

        public DateTime NextRobotsFetch { get { return !LastRobotsFetched.HasValue ? DateTime.MinValue : LastRobotsFetched.Value.AddMinutes(Settings.RobotsCacheTimeoutMinutes); } }

        public bool RobotsNeedsVisiting { get { return DateTime.Now > NextRobotsFetch; } }

        public Site()
        {
            LastRobotsFetched = null;
            IsBlocked = false;
            HostName = String.Empty;
        }

        public Site(Uri anyUri) : this()
        {
            HostName = anyUri?.Host;
        }
    }
}
