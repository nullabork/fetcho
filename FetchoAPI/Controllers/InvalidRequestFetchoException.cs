using Fetcho.Common;
using System;
using System.Runtime.Serialization;

namespace Fetcho.FetchoAPI.Controllers
{
    public class InvalidRequestFetchoException : FetchoException
    {
        public InvalidRequestFetchoException()
        {
        }

        public InvalidRequestFetchoException(string message) : base(message)
        {
        }

        public InvalidRequestFetchoException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidRequestFetchoException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
