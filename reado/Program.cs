using Fetcho.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Fetcho
{
    class Program
    {
        /// <summary>
        /// Reads packets and runs custom classes on them
        /// </summary>
        /// <remarks>
        /// 
        /// USAGE:
        ///   reado [processor] [path] [args...]
        ///   reado --help
        ///   
        /// EXAMPLE:
        ///   reado ExtractLinksConsumer packet-53.xml
        ///   reado ExtractLinksThatMatchConsumer packet-2.xml australia news
        ///
        /// </remarks>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            log4net.Config.XmlConfigurator.Configure();

            // args1 = Packet Procssor
            // args2 = Packet Path

            if (args.Length < 2 || args[0] == "--help")
                OutputHelp();
            else
            {
                string packetProcessorString = args[0];
                string packetPath = args[1];

                var reado = CreateReadoProcessor(packetProcessorString, args);

                if (!packetPath.Contains("*"))
                {
                    reado.Process(packetPath);
                }
                else
                {
                    string path = Path.GetDirectoryName(packetPath);
                    if (String.IsNullOrWhiteSpace(path))
                        path = Environment.CurrentDirectory;

                    string searchPattern = "*";
                    if ( path.Length < packetPath.Length ) searchPattern = packetPath.Substring(path.Length+1);

                    foreach (var file in Directory.GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly))
                        if (Path.GetFileName(file).StartsWith("packet-"))
                            reado.Process(file);
                }
            }
        }

        static ReadoProcessor CreateReadoProcessor(string processorType, string [] args)
        {
            var reado = new ReadoProcessor();
            reado.Processor.Consumer = GetWebDataPacketProcessor(processorType, GetConsumerArgs(args));
            return reado;
        }

        static IEnumerable<string> GetConsumerArgs(string[] args) => new ArraySegment<string>(args, 2, args.Length - 2);

        static IWebDataPacketConsumer GetWebDataPacketProcessor(string typeName, IEnumerable<string> args)
        {
            var type = GetWebDataPacketConsumerTypes().FirstOrDefault(x => x.Name == typeName);

            if (type == null)
                throw new FetchoException("Can't find type with name " + typeName);

            return Activator.CreateInstance(type, args.ToArray()) as IWebDataPacketConsumer;
        }

        static IEnumerable<Type> GetWebDataPacketConsumerTypes()
        {
            var types = typeof(Program).Assembly.GetTypes().
                        Where(t => typeof(IWebDataPacketConsumer).IsAssignableFrom(t));
            return types;
        }

        static void OutputHelp()
        {
            Console.WriteLine("reado [processor] [packet path] [args...]");
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
}
