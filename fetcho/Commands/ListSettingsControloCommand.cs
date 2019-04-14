using Fetcho.Common;
using Fetcho.Common.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Fetcho.Commands
{
    public class ListSettingsControloCommand : ControloCommand
    {
        public override string CommandName => "list";

        public override string ShortHelp => "List all settings";

        public override async Task Execute(string[] args)
        {
            var t = typeof(FetchoConfiguration);
            foreach (var p in t.GetProperties())
            {
                if (p.GetCustomAttributes(typeof(ConfigurationSettingAttribute), false).Any())
                    Controlo.ReportInfo("\t{0,-50} {1,-16} = {2}", p.Name, p.PropertyType, FormatValue(p.GetValue(FetchoConfiguration.Current)));
            }
        }

        private string FormatValue(object value)
        {
            if (value is IEnumerable<object> e)
                return e.Aggregate("", (agg, next) => agg + "," + next);
            return value?.ToString();
        }
    }
}
