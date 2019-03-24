using System;
using System.Runtime.Serialization;

namespace Fetcho.FetchoAPI.Controllers
{
    [Serializable]
    internal class AccessKeyDoesntExistFetchoException : Exception
    {
        public AccessKeyDoesntExistFetchoException()
        {
        }

        public AccessKeyDoesntExistFetchoException(string message) : base(message)
        {
        }

        public AccessKeyDoesntExistFetchoException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AccessKeyDoesntExistFetchoException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}