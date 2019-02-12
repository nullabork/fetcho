using log4net;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fetcho.Common
{
    /// <summary>
    /// Controls the maximum number of tasks that can be waiting and runs the relief function if theres too many waiting according to some predetermined wait value
    /// </summary>
    /// <remarks>NOTE: The wait should not be infinite</remarks>
    public class PressureReliefValve<T> : IDisposable
    {
        static readonly ILog log = LogManager.GetLogger(typeof(PressureReliefValve<T>));

        /// <summary>
        /// Function for the Wait action - note the wait should not be infinite
        /// </summary>
        public Func<T, Task<bool>> WaitFunc { get; set; }

        /// <summary>
        /// Function to release a task or 'exit' the valve
        /// </summary>
        public Action<T> ReleaseAction { get; set; }

        /// <summary>
        /// How pressure is relived
        /// </summary>
        public Action<T> PressureReliefAction { get; set; }

        /// <summary>
        /// # of tasks waiting
        /// </summary>
        public int TasksWaiting { get { return _tasksWaiting; } }
        private int _tasksWaiting = 0;

        /// <summary>
        /// # of tasks in the valve
        /// </summary>
        public int TasksInValve { get { return _tasksInValve; } }
        private int _tasksInValve = 0;

        /// <summary>
        /// Threshold to start relief action
        /// </summary>
        public int WaitingThreshold { get; set; }

        /// <summary>
        /// True if the waiting threshold is exceeded
        /// </summary>
        public bool ThresholdExceeded { get => TasksWaiting >= WaitingThreshold; }

        public PressureReliefValve(int waitingThreshold) => WaitingThreshold = waitingThreshold;

        private PressureReliefValve()
        {
        }

        /// <summary>
        /// Wait to enter the valve
        /// </summary>
        /// <returns>True if successful, false if not</returns>
        public async Task<bool> WaitToEnter(T item)
        {
            if (disposedValue)
                throw new ObjectDisposedException("PressureReliefValve");

            Interlocked.Increment(ref _tasksWaiting);
            while (!await WaitFunc.Invoke(item))
            {
                if (disposedValue)
                    throw new ObjectDisposedException("PressureReliefValve");

                if (ThresholdExceeded)
                {
                    Interlocked.Decrement(ref _tasksWaiting);
                    PressureReliefAction?.Invoke(item);
                    return false;
                }
            }

            Interlocked.Decrement(ref _tasksWaiting);
            Interlocked.Increment(ref _tasksInValve);
            return true;
        }

        /// <summary>
        /// Exit the valve
        /// </summary>
        public void Exit(T item)
        {
            if (disposedValue)
                throw new ObjectDisposedException("PressureReliefValve");

            Interlocked.Decrement(ref _tasksInValve);
            ReleaseAction?.Invoke(item);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion



    }
}
