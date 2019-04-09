using System;

namespace Fetcho.Common
{
    public class QueueItemStatusUpdateEventArgs : EventArgs
    {
        public QueueItem Item;
        public QueueItemStatus Status;
    }
}
