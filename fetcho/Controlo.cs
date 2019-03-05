using Fetcho;
using Fetcho.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fetcho
{
    public class Controlo
    {
        public bool Running { get; set; }

        public Controlo()
        {
            Running = true;
        }

        public async Task Process()
        {
            while (Running)
            {
                try
                {
                    string line = await Console.In.ReadLineAsync().ConfigureAwait(false);

                    if (line.StartsWith("set "))
                        SetSetting(line);
                    else if (line.StartsWith("list"))
                        ListSettings();

                }
                catch (Exception ex)
                {
                    Utility.LogException(ex);
                }
            }
        }

        private void SetSetting(string line)
        {
            string[] tokens = line.Split(' ');

            if (tokens.Length <= 2) return;

            var f = typeof(FetchoConfiguration).GetField(tokens[1]);

            if (f.FieldType == typeof(int))
            {
                if (int.TryParse(tokens[2], out int value))
                    f.SetValue(FetchoConfiguration.Current, value);
            }
            else if (f.FieldType == typeof(string))
            {
                string s = new ArraySegment<string>(tokens, 2, tokens.Length - 2).Aggregate((a, b) => a + " " + b);
                f.SetValue(FetchoConfiguration.Current, s);
            }
            else if (f.FieldType == typeof(decimal))
            {
                if (decimal.TryParse(tokens[2], out decimal value))
                    f.SetValue(FetchoConfiguration.Current, value);
            }
            else if (f.FieldType == typeof(bool))
            {
                if (bool.TryParse(tokens[2], out bool value))
                    f.SetValue(FetchoConfiguration.Current, value);
            }

        }

        private void ListSettings()
        {
            var t = typeof(FetchoConfiguration);
            foreach (var f in t.GetFields())
            {
                //if (f.GetCustomAttributes(typeof(ConfigurationSettings), false).Any())
                    Console.WriteLine("\t{0}\t{1}\t = {2}", f.Name, f.FieldType, f.GetValue(FetchoConfiguration.Current));
            }
        }

    }
}
