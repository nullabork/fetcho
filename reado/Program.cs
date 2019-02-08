using Fetcho.Common;
using System.IO;

namespace Fetcho
{
    class Program
    {
        static void Main(string[] args)
        {
            // args1 = Packet Procssor
            // args2 = Packet Path

            string packetProcessorString = args[0];
            string packetPath = args[1];

            var reado = new ReadoProcessor();
            reado.Process(packetPath);
        }
    }

    public class ReadoProcessor
    {
        private WebDataPacketProcessor Processor;

        public ReadoProcessor()
        {
            Processor = new WebDataPacketProcessor();
        }

        public void Process(string filepath)
        {
            Processor.Consumer = new ExceptionWebDataPacketConsumer();

            using (var fs = new FileStream(filepath, FileMode.Open))
            {
                var packet = new WebDataPacketReader(fs);
                Processor.Process(packet);
            }
        }
    }
}
