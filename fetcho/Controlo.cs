using Fetcho.Common;
using Fetcho.Common.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Fetcho
{
    public class Controlo
    {
        public bool Running { get; set; }

        public Dictionary<string, ControloCommand> Commands { get; }

        public Controlo()
        {
            Running = true;
            Commands = new Dictionary<string, ControloCommand>();
            RegisterAllCommands();
            FetchoConfiguration.Current.ConfigurationChange += (sender, e) 
                => ReportInfo("Configuration setting {0} changed from {1} to {2}",
                                     e.PropertyName, e.OldValue, e.NewValue);
        }

        public async Task Process()
        {
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
                        try
                        {
                            Commands[commandName].Execute(this, new ArraySegment<string>(tokens, 1, tokens.Length - 1).ToArray());
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

        public void ReportError(string format, params object[] args) => Console.WriteLine(format, args);

        public void ReportInfo(string format, params object[] args) => Console.WriteLine(format, args);

        private void RegisterAllCommands()
        {
            foreach( var t in GetType().Assembly.GetTypes())
            {
                if (!t.IsAbstract && t.IsSubclassOf(typeof(ControloCommand)))
                {
                    var cinfo = t.GetConstructor(new Type[]{ });
                    ControloCommand o = cinfo.Invoke(null) as ControloCommand;
                    Commands.Add(o.CommandName, o);
                }
            }
        }
    }

    public abstract class ControloCommand
    {
        public abstract string CommandName { get; }

        public virtual string ShortHelp { get => "Shorthelp not set"; }

        public abstract void Execute(Controlo controlo, string[] args);
    }

    public class QuitControloCommand : ControloCommand
    {
        public override string CommandName => "quit";

        public override string ShortHelp => "Exit the program hard"; 

        public override void Execute(Controlo controlo, string[] args)
        {
            controlo.ReportInfo("Quitting");
            Environment.Exit(1);
        }
    }

    public class HelpControloCommand : ControloCommand
    {
        public override string CommandName => "help";

        public override string ShortHelp => "List all commands";

        public override void Execute(Controlo controlo, string[] args)
        {
            foreach( var command in controlo.Commands.Values )
            {
                controlo.ReportInfo("{0,10}\t{1}", command.CommandName, command.ShortHelp);
            }
        }
    }

    public class ListSettingsControloCommand : ControloCommand
    {
        public override string CommandName => "list";

        public override string ShortHelp => "List all settings";

        public override void Execute(Controlo controlo, string[] args)
        {
            var t = typeof(FetchoConfiguration);
            foreach (var p in t.GetProperties())
            {
                if (p.GetCustomAttributes(typeof(ConfigurationSettingAttribute), false).Any())
                    controlo.ReportInfo("\t{0,-50} {1,-16} = {2}", p.Name, p.PropertyType, p.GetValue(FetchoConfiguration.Current));
            }
        }
    }

    public class SetSettingControloCommand : ControloCommand
    {
        public override string CommandName => "set";

        public override string ShortHelp => "set [settingName] [settingValue]";

        public override void Execute(Controlo controlo, string[] args)
        {
            if ( args.Length < 2 )
            {
                controlo.ReportError("Usage: {0}", ShortHelp);
                return;
            }

            string settingName = args[0];
            string settingValue = new ArraySegment<string>(args, 1, args.Length - 1).Aggregate((a, b) => a + " " + b);

            var p = typeof(FetchoConfiguration).GetProperty(settingName);

            if (p == null)
            {
                controlo.ReportError("Setting doesn't exist {0}", settingName);
            }
            else
            {
                var converter = TypeDescriptor.GetConverter(p.PropertyType);
                try
                {
                    var obj = converter.ConvertFromString(null, CultureInfo.InvariantCulture, settingValue);
                    FetchoConfiguration.Current.SetConfigurationSetting(p.Name, obj);
                }
                catch (NotSupportedException)
                {
                    controlo.ReportError("Not a valid {0}", p.PropertyType);
                }
            }
        }
    }
}
