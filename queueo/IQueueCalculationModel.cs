using System.Collections.Generic;
using System.Threading.Tasks;
using Fetcho.Common;

namespace Fetcho.queueo
{
    /// <summary>
    /// Model used to calculate 
    /// </summary>
    interface IQueuePriorityCalculationModel
    {
        /// <summary>
        /// Calculate the priorities for queue items
        /// </summary>
        Task CalculatePriority(IEnumerable<QueueItem> items);
    }
}

