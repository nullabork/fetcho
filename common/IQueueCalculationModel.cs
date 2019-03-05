using System.Collections.Generic;
using System.Threading.Tasks;
using Fetcho.Common;

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

