using Fetcho.Common;
using System;
using System.Runtime.Serialization;

namespace Fetcho.FetchoAPI.Controllers
{
    public class PermissionDeniedFetchoException : FetchoException
    {
        public PermissionDeniedFetchoException() { }

        public PermissionDeniedFetchoException(string message) : base(message) { }

        public PermissionDeniedFetchoException(string message, Exception innerException) : base(message, innerException) { }

        public PermissionDeniedFetchoException(string format, params object[] args) : base(string.Format(format, args)) { }

        protected PermissionDeniedFetchoException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
