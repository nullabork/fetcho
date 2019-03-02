using System;
using System.Threading.Tasks.Dataflow;

namespace Fetcho.Common.DataFlow
{
    public class BufferBlockObjectConsoleWriter<T> : BufferBlockObjectConsumer<T>
    {
        public BufferBlockObjectConsoleWriter(BufferBlock<T> inBuffer) : base(Console.Out, inBuffer)
        {
        }
    }
}
