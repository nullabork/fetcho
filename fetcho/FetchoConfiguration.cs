using Fetcho.Common;

namespace Fetcho
{
    public class FetchoConfiguration
    {
        public int PressureReliefThreshold = 2000 * 5 / 10; // if it totally fills up it'll chuck some out
        public int HowOftenToReportStatusInMilliseconds = 30000;
        public int TaskStartupWaitTimeInMilliseconds = 360000;
        public int MinPressureReliefValveWaitTimeInMilliseconds = Settings.MaximumFetchSpeedMilliseconds * 2;
        public int MaxPressureReliefValveWaitTimeInMilliseconds = Settings.MaximumFetchSpeedMilliseconds * 12;
        public int MaximumResourcesPerDataPacket = 100000;
        public int DatabasePoolSize = 5;

        /// <summary>
        /// Init all lists to this
        /// </summary>
        public int MaxConcurrentTasks = 250;

        /// <summary>
        /// Queue items with a number higher than this will be rejected 
        /// </summary>
        public uint MaximumPriorityValueForLinks = 740 * 1000 * 1000;

        /// <summary>
        /// Number of items that can be in a chunk
        /// </summary>
        public int MaximumChunkSize = 1000;

        const int MaxConcurrentFetchesDefault = 2000;
        public int MaxConcurrentFetches = MaxConcurrentFetchesDefault;

        /// <summary>
        /// Maximum number of concurrent fetches, times the number of items able to be fetched before the fetch timeout
        /// Coupled with some fuziness for half queues meaning the IP may arrive in two queues sooner
        /// </summary>
        public int WindowForIPsSeenRecently = MaxConcurrentFetchesDefault * 4;

        /// <summary>
        /// Maximum links that can be output
        /// </summary>
        public int MaximumLinkQuota = 400000;

        /// <summary>
        /// Enable the quota
        /// </summary>
        public bool QuotaEnabled = false;

        public string DataSourcePath { get; set; }

        public IBlockProvider BlockProvider { get; set; }

        public IQueuePriorityCalculationModel QueueOrderingModel { get; set; }

        public FetchoConfiguration() { }
    }
}

