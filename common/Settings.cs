namespace Fetcho.Common
{
    /// <summary>
    /// Description of Settings.
    /// </summary>
    public class Settings
    {
        public const string UserAgent = "ResearchBot";

        public const int HostCacheManagerMaxInMemoryDomainRecords = 10000;
        public const int MaximumFetchSpeedMilliseconds = 6000;
        public const int RobotsCacheTimeoutMinutes = 480;
        public const int MaxFileDownloadLengthInBytes = 16 * 1024 * 1024;


        public Settings()
        {
        }
    }
}
