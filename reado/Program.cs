using Fetcho.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Fetcho
{
    class Program
    {
        static void Main(string[] args)
        {
            // args1 = Packet Procssor
            // args2 = Packet Path

            if (args[0] == "--help")
                OutputHelp();
            else
            {
                string packetProcessorString = args[0];
                string packetPath = args[1];

                var reado = new ReadoProcessor();
                reado.Processor.Consumer = GetWebDataPacketProcessor(packetProcessorString);

                if (!File.Exists(packetPath))
                    throw new FileNotFoundException(packetPath);

                reado.Process(packetPath);
            }
        }

        static IWebDataPacketConsumer GetWebDataPacketProcessor(string typeName)
        {
            var type = GetWebDataPacketConsumerTypes().FirstOrDefault(x => x.Name == typeName);

            if (type == null)
                throw new Exception("Can't find type with name " + typeName);

            return type.GetConstructor(new Type[] { }).Invoke(null) as IWebDataPacketConsumer;
        }

        static IEnumerable<Type> GetWebDataPacketConsumerTypes()
        {
            var types = typeof(Program).Assembly.GetTypes().
                        Where(t => typeof(IWebDataPacketConsumer).IsAssignableFrom(t));
            return types;
        }

        static void OutputHelp()
        {
            Console.WriteLine("reado [processor] [packet path]");
            Console.WriteLine("reado --help");
            Console.WriteLine();
            Console.WriteLine("Processors available:");
            OutputProcessors();
        }

        static void OutputProcessors()
        {
            var types = GetWebDataPacketConsumerTypes();

            foreach (var type in types)
                Console.WriteLine("{0}", type.Name);
        }
    }

    public class ReadoProcessor
    {
        public WebDataPacketProcessor Processor { get;  }

        public ReadoProcessor()
        {
            Processor = new WebDataPacketProcessor();
        }

        public void Process(string filepath)
        {
            using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var packet = new WebDataPacketReader(fs);
                Processor.Process(packet);
            }
        }
    }
}
