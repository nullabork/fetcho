using Fetcho.Commands;
using Fetcho.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Fetcho
{
    public class Controlo
    {
        public bool Running { get; set; }

        public Dictionary<string, ControloCommand> Commands { get; }

        public ITargetBlock<IEnumerable<QueueItem>> PrioritisationBufferIn;
        public ITargetBlock<IEnumerable<QueueItem>> FetchQueueBufferOut;
        public BufferBlock<IWebResourceWriter> DataWriterPool;

        public Controlo(
            ITargetBlock<IEnumerable<QueueItem>> prioritisationBufferIn,
            ITargetBlock<IEnumerable<QueueItem>> fetchQueueBufferOut,
            BufferBlock<IWebResourceWriter> dataWriterPool)
        {
            PrioritisationBufferIn = prioritisationBufferIn;
            FetchQueueBufferOut = fetchQueueBufferOut;
            DataWriterPool = dataWriterPool;
            Running = true;
            Commands = new Dictionary<string, ControloCommand>();
            RegisterAllCommands();
            FetchoConfiguration.Current.ConfigurationChange += (sender, e) 
                => ReportInfo("Configuration setting {0} changed from {1} to {2}",
                                     e.PropertyName, e.OldValue, e.NewValue);
        }

        public async Task Process()
        {
            Console.WriteLine("Type 'help' for a list of commands");

            while (Running)
            {
                try
                {
                    string line = await Console.In.ReadLineAsync().ConfigureAwait(false);

                    string[] tokens = line.Split(' ');

                    if (tokens.Length == 0) return;

                    string commandName = tokens[0].ToLower();

                    if ( Commands.ContainsKey(commandName))
                    {
                        var args = new ArraySegment<string>(tokens, 1, tokens.Length - 1).ToArray();
                        Utility.LogInfo("Command: {0} {1}", commandName, args.Aggregate(String.Empty, (x, y) => x + " " + y));
                        try
                        {
                            await Commands[commandName].Execute(args);
                        }
                        catch( Exception ex)
                        {
                            Utility.LogException(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utility.LogException(ex);
                }
            }
        }

        public void ReportError(string format, params object[] args) 
            => Console.WriteLine(format, args);

        public void ReportInfo(string format, params object[] args) 
            => Console.WriteLine(format, args);

        private void RegisterAllCommands()
        {
            foreach( var t in GetType().Assembly.GetTypes())
            {
                if (!t.IsAbstract && t.IsSubclassOf(typeof(ControloCommand)))
                {
                    var cinfo = t.GetConstructor(new Type[]{ });
                    ControloCommand o = cinfo.Invoke(null) as ControloCommand;
                    o.Controlo = this;
                    Commands.Add(o.CommandName, o);
                }
            }
        }
    }
}
