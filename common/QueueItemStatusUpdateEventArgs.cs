using System;
using Fetcho.Common;

namespace Fetcho.Common
{
    public class QueueItemStatusUpdateEventArgs : EventArgs
    {
        public QueueItem Item;
        public QueueItemStatus Status;
    }
}
