
using System;

namespace Fetcho.Common.Entities
{
    public class Site
    {
        public MD5Hash Hash { get => MD5Hash.Compute(HostName);  }

        public string HostName { get; set; }

        public bool IsBlocked { get; set; }

        public DateTime? LastRobotsFetched { get; set; }

        public RobotsFile RobotsFile { get; set; }

        public DateTime NextRobotsFetch { get => !LastRobotsFetched.HasValue ? DateTime.MinValue : LastRobotsFetched.Value.AddDays(FetchoConfiguration.Current.RobotsCacheTimeoutDays);  }

        public bool RobotsNeedsVisiting { get => DateTime.UtcNow > NextRobotsFetch;  }

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
