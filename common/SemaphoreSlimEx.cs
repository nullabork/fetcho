using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Fetcho.Common
{
    /// <summary>
    /// Extension methods for SemaphoreSlim
    /// </summary>
    public static class SemaphoreSlimEx
    {
        /// <summary>
        /// Increase or decrease the number of threads that can simultaneously access the resource
        /// </summary>
        /// <param name="semaphore"></param>
        /// <param name="newMaximumCount"></param>
        public static async Task ReleaseOrReduce(this SemaphoreSlim semaphore, int oldInitialCount, int newInitialCount)
        {
            int difference = newInitialCount - oldInitialCount;

            if (difference == 0)
                return;
            else if ( difference > 0 )
            {
                semaphore.Release(difference);
            }
            else
            {
                var l = new List<Task>();
                while(difference < 0)
                {
                    l.Add(semaphore.WaitAsync());
                    difference++;
                }
                await Task.WhenAll(l.ToArray());
            }
        }
    }
}