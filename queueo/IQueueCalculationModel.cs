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
        /// Calculate the sequence number for a queue item
        /// </summary>
        /// <param name="item"></param>
        /// <returns>An async task - note the object is irrelevant the method should set the Sequence value on the QueueItem passed in</returns>
        Task CalculatePriority(QueueItem item);
    }
}

