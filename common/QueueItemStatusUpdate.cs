namespace Fetcho.Common
{
    public enum QueueItemStatus
    {
        Unknown,
        Duplicate,
        Queuing,
        Fetching,
        Discarded,
        Waiting,
        Fetched,
        Max
    }
}
