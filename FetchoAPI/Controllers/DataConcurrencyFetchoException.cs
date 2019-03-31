using System;
using System.Runtime.Serialization;

namespace Fetcho.FetchoAPI.Controllers
{
    [Serializable]
    internal class DataConcurrencyFetchoException : Exception
    {
        public DataConcurrencyFetchoException()
        {
        }

        public DataConcurrencyFetchoException(string message) : base(message)
        {
        }

        public DataConcurrencyFetchoException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected DataConcurrencyFetchoException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}