using Fetcho.Common;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Fetcho.Commands
{
    public class SetSettingControloCommand : ControloCommand
    {
        public override string CommandName => "set";

        public override string ShortHelp => "set [settingName] [settingValue]";

        public override async Task Execute(string[] args)
        {
            if ( args.Length < 2 )
            {
                Controlo.ReportError("Usage: {0}", ShortHelp);
                return;
            }

            string settingName = args[0];
            string settingValue = new ArraySegment<string>(args, 1, args.Length - 1).Aggregate((a, b) => a + " " + b);

            var p = typeof(FetchoConfiguration).GetProperty(settingName);

            if (p == null)
            {
                Controlo.ReportError("Setting doesn't exist {0}", settingName);
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
                    Controlo.ReportError("Not a valid {0}", p.PropertyType);
                }
            }
        }
    }
}
