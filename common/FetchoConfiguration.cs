using Fetcho.Common.Configuration;
using System;
using System.Linq.Expressions;

namespace Fetcho.Common
{
    public class FetchoConfiguration
    {
        public event EventHandler<ConfigurationChangeEventArgs> ConfigurationChange;

        protected void OnConfigurationChange(string propertyName, Type propertyType, object oldValue, object newValue)
        {
            var eventHandler = ConfigurationChange;

            if ( eventHandler != null )
                ConfigurationChange(this, new ConfigurationChangeEventArgs(propertyName, propertyType, oldValue, newValue));
        }

        const int DefaultMaxFetchSpeedInMilliseconds = 10000;
        const int DefaultMaxConcurrentFetches = 2000;

        [ConfigurationSetting("ResearchBot 0.2")]
        public string UserAgent { get; private set; }

        [ConfigurationSetting(10000)]
        public int HostCacheManagerMaxInMemoryDomainRecords { get; private set; }

        [ConfigurationSetting(10000)]
        public int MaxFetchSpeedInMilliseconds { get; private set; }

        [ConfigurationSetting(28)]
        public int RobotsCacheTimeoutDays { get; private set; }

        [ConfigurationSetting(1 * 1024 * 1024)]
        public int MaxFileDownloadLengthInBytes { get; private set; }

        [ConfigurationSetting(120000)]
        public int ResponseReadTimeoutInMilliseconds { get; private set; }

        [ConfigurationSetting(28)]
        public int PageCacheExpiryInDays { get; private set; }

        [ConfigurationSetting(30000)]
        public int RequestTimeoutInMilliseconds { get; private set; }


        [ConfigurationSetting(30000)]
        public int HowOftenToReportStatusInMilliseconds { get; private set; }

        [ConfigurationSetting(360000)]
        public int TaskStartupWaitTimeInMilliseconds { get; private set; }

        [ConfigurationSetting(DefaultMaxFetchSpeedInMilliseconds * 2)]
        public int MinPressureReliefValveWaitTimeInMilliseconds { get; private set; }

        [ConfigurationSetting(DefaultMaxFetchSpeedInMilliseconds * 12)]
        public int MaxPressureReliefValveWaitTimeInMilliseconds { get; private set; }

        [ConfigurationSetting(100000)]
        public int MaxResourcesPerDataPacket { get; private set; }

        [ConfigurationSetting(500)]
        public int MaxConcurrentTasks { get; private set; }

        [ConfigurationSetting(1000000)]
        public int DuplicateLinkCacheWindowSize { get; private set; }

        /// <summary>
        /// Queue items with a number higher than this will be rejected 
        /// </summary>
        [ConfigurationSetting((uint)740 * 1000 * 1000)]
        public uint MaxPriorityValueForLinks { get; private set; }

        [ConfigurationSetting(1000)]
        public int MaxQueueBufferQueueLength { get; private set; }

        [ConfigurationSetting(50)]
        public int MaxQueueBufferQueues { get; private set; }


        [ConfigurationSetting(DefaultMaxConcurrentFetches)]
        public int MaxConcurrentFetches { get; private set; }

        [ConfigurationSetting(DefaultMaxConcurrentFetches * 5 / 10)] // 50% of max
        public int PressureReliefThreshold { get; private set; }

        [ConfigurationSetting(10000)]
        public int MaxLinksToExtractFromOneResource { get; set; }

        /// <summary>
        /// Maximum number of concurrent fetches, times the number of items able to be fetched before the fetch timeout
        /// Coupled with some fuziness for half queues meaning the IP may arrive in two queues sooner
        /// </summary>
        [ConfigurationSetting(DefaultMaxConcurrentFetches * 6)]
        public int WindowForIPsSeenRecently { get; private set; }

        /// <summary>
        /// Maximum links that can be output
        /// </summary>
        [ConfigurationSetting(400000)]
        public int MaxLinkQuota { get; private set; }

        /// <summary>
        /// Enable the quota
        /// </summary>
        [ConfigurationSetting(false)]
        public bool QuotaEnabled { get; private set; }

        [ConfigurationSetting]
        public string DataSourcePath { get; private set; }

        [ConfigurationSetting(@"G:\fetcho\data\GeoLite2-City.mmdb")]
        public string GeoIP2CityDatabasePath { get; private set; }

        [ConfigurationSetting]
        public string FetchoWorkspaceServerBaseUri { get; private set; }

        public IBlockProvider BlockProvider { get; set; }

        public IQueuePriorityCalculationModel QueueOrderingModel { get; set; }

        public HostCacheManager HostCache { get; set; }

        public FetchoConfiguration()
        {
            InitialiseToDefaults();
        }

        /// <summary>
        /// Current configuration in force
        /// </summary>
        public static FetchoConfiguration Current { get; set; }

        public void InitialiseToDefaults()
        {
            var props = GetType().GetProperties();
            foreach (var prop in props)
            {
                var attrs = prop.GetCustomAttributes(typeof(ConfigurationSettingAttribute), false);
                if (attrs.Length > 0)
                {
                    var attr = attrs[0] as ConfigurationSettingAttribute;
                    if (attr.Default != null)
                        prop.SetValue(this, attr.Default);
                }
            }
        }

        public void SetConfigurationSetting(string propertyName, object value)
        {
            var prop = this.GetType().GetProperty(propertyName);
            if (prop == null)
                throw new FetchoException("Property does't exist");
            if (value.GetType() != prop.PropertyType)
                throw new FetchoException("Value type doesn't match property type");

            var oldValue = prop.GetValue(this);
            prop.SetValue(this, value);
            OnConfigurationChange(propertyName, prop.PropertyType, oldValue, value);
        }

        public void SetConfigurationSetting<T>(Expression<Func<T>> propertyLambda, T value) =>
            SetConfigurationSetting(Utility.GetPropertyName(propertyLambda), value);
    }
}

