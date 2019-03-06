using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Fetcho.Common.DataFlow
{
    /// <summary>
    /// Consume the objects from the buffer and write them out to writer
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BufferBlockObjectConsumer<T> : IDisposable
    {
        private BufferBlock<T> InBuffer;
        private bool running = true;
        private TextWriter Writer = null;
        private int count = 0;

        public BufferBlockObjectConsumer(TextWriter writer, BufferBlock<T> inBuffer)
        {
            InBuffer = inBuffer;
            Writer = writer;
        }

        public async Task Process()
        {
            while (running)
            {
                T o = await InBuffer.ReceiveAsync().ConfigureAwait(false);
                if (o is IEnumerable)
                    foreach (var oo in (o as IEnumerable))
                        await Writer?.WriteLineAsync(oo.ToString());
                else
                    await Writer?.WriteLineAsync(o.ToString());

                unchecked
                {
                    if (count++ % 1000 == 0)
                        await Writer?.FlushAsync();
                }
            }
        }

        public void Shutdown() => running = false;

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Writer?.Dispose();
                }

                Writer = null;
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

    }
}
