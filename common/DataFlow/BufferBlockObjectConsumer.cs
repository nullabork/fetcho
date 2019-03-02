using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Fetcho.Common.DataFlow
{
    /// <summary>
    /// Consume the objects from the buffer and write them out to writer
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BufferBlockObjectConsumer<T>
    {
        private BufferBlock<T> InBuffer;
        private bool running = true;
        private TextWriter Writer = null;

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
                await Writer?.WriteLineAsync(o.ToString());
            }
        }

        public void Shutdown() => running = false;

    }
}
