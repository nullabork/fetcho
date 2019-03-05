using Fetcho.Common;
using Fetcho.Common.Configuration;

namespace Fetcho.Common
{
    public class FetchoConfiguration
    {
        const int MaxConcurrentFetchesDefault = 1000;
        const int MaximumFetchSpeedInMillisecondsDefault = 10000;

        [ConfigurationSetting]
        public string UserAgent = "ResearchBot 0.2";

        [ConfigurationSetting]
        public int HostCacheManagerMaxInMemoryDomainRecords = 10000;

        [ConfigurationSetting]
        public int MaximumFetchSpeedMilliseconds = MaximumFetchSpeedInMillisecondsDefault;

        [ConfigurationSetting]
        public int RobotsCacheTimeoutMinutes = 60 * 24 * 28; // 4 weeks

        [ConfigurationSetting]
        public int MaxFileDownloadLengthInBytes = 1 * 1024 * 1024;

        [ConfigurationSetting]
        public int ResponseReadTimeoutInMilliseconds = 120000;

        [ConfigurationSetting]
        public int PageCacheExpiryInDays = 7;

        [ConfigurationSetting]
        public int DefaultRequestTimeoutInMilliseconds = 30000;


        [ConfigurationSetting]
        public int HowOftenToReportStatusInMilliseconds = 30000;

        [ConfigurationSetting]
        public int TaskStartupWaitTimeInMilliseconds = 360000;

        [ConfigurationSetting]
        public int MinPressureReliefValveWaitTimeInMilliseconds = MaximumFetchSpeedInMillisecondsDefault * 2;

        [ConfigurationSetting]
        public int MaxPressureReliefValveWaitTimeInMilliseconds = MaximumFetchSpeedInMillisecondsDefault * 12;

        [ConfigurationSetting]
        public int MaximumResourcesPerDataPacket = 100000;

        /// <summary>
        /// Init all lists to this
        /// </summary>
        [ConfigurationSetting]
        public int MaxConcurrentTasks = 500;

        /// <summary>
        /// Queue items with a number higher than this will be rejected 
        /// </summary>
        [ConfigurationSetting]
        public uint MaximumPriorityValueForLinks = 740 * 1000 * 1000;

        [ConfigurationSetting]
        public int MaxQueueBufferQueueLength = 1000;

        [ConfigurationSetting]
        public int MaxQueueBufferQueues = 50;


        [ConfigurationSetting]
        public int MaxConcurrentFetches = MaxConcurrentFetchesDefault;

        [ConfigurationSetting]
        public int PressureReliefThreshold = MaxConcurrentFetchesDefault * 5 / 10; // 50% of max

        /// <summary>
        /// Maximum number of concurrent fetches, times the number of items able to be fetched before the fetch timeout
        /// Coupled with some fuziness for half queues meaning the IP may arrive in two queues sooner
        /// </summary>
        [ConfigurationSetting]
        public int WindowForIPsSeenRecently = MaxConcurrentFetchesDefault * 6;

        /// <summary>
        /// Maximum links that can be output
        /// </summary>
        [ConfigurationSetting]
        public int MaximumLinkQuota = 400000;

        /// <summary>
        /// Enable the quota
        /// </summary>
        [ConfigurationSetting]
        public bool QuotaEnabled = false;

        [ConfigurationSetting]
        public string DataSourcePath;

        public IBlockProvider BlockProvider { get; set; }

        public IQueuePriorityCalculationModel QueueOrderingModel { get; set; }

        public HostCacheManager HostCache { get; set; }

        public FetchoConfiguration() { }

        /// <summary>
        /// Current configuration in force
        /// </summary>
        public static FetchoConfiguration Current { get; set; }
    }
}

