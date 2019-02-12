namespace Fetcho.Common
{
    /// <summary>
    /// Description of Settings.
    /// </summary>
    public class Settings
    {
        public const string UserAgent = "ResearchBot 0.1";

        public const int HostCacheManagerMaxInMemoryDomainRecords = 10000;
        public const int MaximumFetchSpeedMilliseconds = 10000;
        public const int RobotsCacheTimeoutMinutes = 60*24*28; // 4 weeks
        public const int MaxFileDownloadLengthInBytes = 1 * 1024 * 1024;
        
        public Settings()
        {
        }
    }
}
