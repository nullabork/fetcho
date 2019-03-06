using System;
using System.Linq.Expressions;

namespace Fetcho.Common
{
    public class ConfigurationChangeEventArgs : EventArgs
    {
        public string PropertyName { get; }
        public Type PropertyType { get; }
        public object OldValue { get; }
        public object NewValue { get; }

        public ConfigurationChangeEventArgs(string propertyName, Type propertyType, object oldValue, object newValue)
        {
            PropertyName = propertyName;
            PropertyType = propertyType;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public void IfPropertyIs<T>(Expression<Func<T>> propertyLambda, Action executeAction)
        {
            if (Utility.GetPropertyName(propertyLambda) == PropertyName)
                executeAction();
        }

    }
}

