using Fetcho.Common;

namespace Fetcho
{
    public class FetchoConfiguration
    {
        public int MaxConcurrentFetches = 2000;
        public int PressureReliefThreshold = 2000 * 5 / 10; // if it totally fills up it'll chuck some out
        public int HowOftenToReportStatusInMilliseconds = 30000;
        public int TaskStartupWaitTimeInMilliseconds = 360000;
        public int MinPressureReliefValveWaitTimeInMilliseconds = Settings.MaximumFetchSpeedMilliseconds * 2;
        public int MaxPressureReliefValveWaitTimeInMilliseconds = Settings.MaximumFetchSpeedMilliseconds * 12;
        public int MaximumResourcesPerDataPacket = 100000;

        public string DataSourcePath { get; set; }

        public IBlockProvider BlockProvider { get; set; }

        public FetchoConfiguration() { }
    }
}

