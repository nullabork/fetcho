using System;

namespace Fetcho.Common.Configuration
{
    public class ConfigurationSettingAttribute : Attribute
    {
        public object Default { get; set; }

        public ConfigurationSettingAttribute(object defaultValue) => Default = defaultValue;

        public ConfigurationSettingAttribute() => Default = null;
    }
}
