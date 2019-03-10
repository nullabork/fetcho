using System;
using System.Runtime.Serialization;

namespace Fetcho.Common.Entities
{
    [Serializable]
    internal class InvalidObjectFetchoException : Exception
    {
        public InvalidObjectFetchoException()
        {
        }

        public InvalidObjectFetchoException(string message) : base(message)
        {
        }

        public InvalidObjectFetchoException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidObjectFetchoException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}