using Fetcho.Common;
using Fetcho.Common.Configuration;
using System.Linq;

namespace Fetcho.Commands
{
    public class ListSettingsControloCommand : ControloCommand
    {
        public override string CommandName => "list";

        public override string ShortHelp => "List all settings";

        public override void Execute(string[] args)
        {
            var t = typeof(FetchoConfiguration);
            foreach (var p in t.GetProperties())
            {
                if (p.GetCustomAttributes(typeof(ConfigurationSettingAttribute), false).Any())
                    Controlo.ReportInfo("\t{0,-50} {1,-16} = {2}", p.Name, p.PropertyType, p.GetValue(FetchoConfiguration.Current));
            }
        }
    }
}
