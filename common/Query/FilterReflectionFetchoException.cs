
using System;
using System.Runtime.Serialization;

namespace Fetcho.Common
{
    [Serializable]
    internal class FilterReflectionFetchoException : FetchoException
    {
        public FilterReflectionFetchoException()
        {
        }

        public FilterReflectionFetchoException(string message) : base(message)
        {
        }

        public FilterReflectionFetchoException(string format, params object[] args) : base(format, args) { }

        public FilterReflectionFetchoException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected FilterReflectionFetchoException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
