using System.Collections.Generic;

namespace Fetcho.Common
{
    /// <summary>
    /// Model used to calculate 
    /// </summary>
    public interface IQueuePriorityCalculationModel
    {
        /// <summary>
        /// Calculate the priorities for queue items
        /// </summary>
        void CalculatePriority(IEnumerable<QueueItem> items);
    }
}

