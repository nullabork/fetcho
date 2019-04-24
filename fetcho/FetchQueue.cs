using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Fetcho.Common;

namespace Fetcho
{
    public class FetchQueue : IDisposable
    {
        private Dictionary<string, Queue<QueueItem>> Queue { get; set; }
        private readonly object _fetchQueueLock = new object();
        private readonly SemaphoreSlim fetchQueueLock = new SemaphoreSlim(1);

        public FetchQueue()
        {
            Queue = new Dictionary<string, Queue<QueueItem>>();
        }

        public async Task<bool> Enqueue(IEnumerable<QueueItem> items)
        {
            if (!items.Any()) return false;

            bool created = false;
            var ipAddress = await GetQueueItemTargetIP(items.First());
            string key = ipAddress.ToString();

            try
            {
                await fetchQueueLock.WaitAsync();

                if (!Queue.ContainsKey(key))
                {
                    var q = new Queue<QueueItem>();
                    Queue.Add(key, q);
                    created = true;
                }
                foreach (var item in items)
                    Queue[key].Enqueue(item);
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
            finally
            {
                fetchQueueLock.Release();
            }
            return created;
        }

        public async Task<QueueItem> Dequeue(IPAddress ipAddress)
        {
            QueueItem item = null;
            string key = ipAddress.ToString();
            bool deleteKey = false;

            try
            {
                await fetchQueueLock.WaitAsync();

                if (Queue.ContainsKey(key))
                {
                    item = Queue[key].Dequeue();
                    deleteKey = (Queue[key].Count == 0);
                }
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
            finally
            {
                fetchQueueLock.Release();
            }

            if (deleteKey)
            {
                await Task.Delay(FetchoConfiguration.Current.FetchQueueEmptyWaitTimeout); // wait five seconds before deleting
                try
                {
                    await fetchQueueLock.WaitAsync();

                    if (Queue[key].Count == 0)
                        Queue.Remove(key);
                }
                catch (Exception ex)
                {
                    Utility.LogException(ex);
                }
                finally
                {
                    fetchQueueLock.Release();
                }
            }

            return item;
        }

        public async Task RemoveQueue(IPAddress ipAddress)
        {
            string key = ipAddress.ToString();

            try
            {
                await fetchQueueLock.WaitAsync();

                if (Queue.ContainsKey(key))
                    Queue.Remove(key);
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
            finally
            {
                fetchQueueLock.Release();
            }
        }

        public async ValueTask<int> QueueCount(IPAddress ipAddress)
        {
            string key = ipAddress.ToString();

            try
            {
                await fetchQueueLock.WaitAsync();

                if (Queue.ContainsKey(key))
                    return Queue[key].Count;
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
            finally
            {
                fetchQueueLock.Release();
            }

            return 0;
        }

        private async Task<IPAddress> GetQueueItemTargetIP(QueueItem item)
            => item.TargetIP != null && !item.TargetIP.Equals(IPAddress.None) ? item.TargetIP : await Utility.GetHostIPAddress(item.TargetUri);

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    fetchQueueLock.Dispose();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }
}

