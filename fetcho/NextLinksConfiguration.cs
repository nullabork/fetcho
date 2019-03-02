
using System;

namespace Fetcho
{
    public class NextLinksConfiguration
	{
        /// <summary>
        /// Init all lists to this
        /// </summary>
        public int MaxConcurrentTasks = 250;

        /// <summary>
        /// Queue items with a number higher than this will be rejected 
        /// </summary>
        public uint MaximumPriorityValueForLinks = 740 * 1000 * 1000;

        /// <summary>
        /// 
        /// </summary>
        public int HowOftenToReportStatusInMilliseconds = 30000;

        /// <summary>
        /// Number of items that can be in a chunk
        /// </summary>
        public int MaximumChunkSize = 300;

        const int MaxConcurrentFetchesDefault = 2000;
        public int MaxConcurrentFetches = MaxConcurrentFetchesDefault;

        /// <summary>
        /// Maximum number of concurrent fetches, times the number of items able to be fetched before the fetch timeout
        /// Coupled with some fuziness for half queues meaning the IP may arrive in two queues sooner
        /// </summary>
        public int WindowForIPsSeenRecently = MaxConcurrentFetchesDefault * 16;

        /// <summary>
        /// Maximum links that can be output
        /// </summary>
        public int MaximumLinkQuota = 400000;

        /// <summary>
        /// Enable the quota
        /// </summary>
        public bool QuotaEnabled = false;
	}
}

