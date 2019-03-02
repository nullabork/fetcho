using System.Threading.Tasks.Dataflow;

namespace Fetcho.Common.DataFlow
{
    /// <summary>
    /// Eats all the items on the queue without putting them anywhere
    /// </summary>
    public class ThrowAwayBufferBlockObjectConsumer<T> : BufferBlockObjectConsumer<T>
    {
        public ThrowAwayBufferBlockObjectConsumer(BufferBlock<T> inBuffer) : base(null, inBuffer)
        {

        }
    }
}
