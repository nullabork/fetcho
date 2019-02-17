using System;
using System.Runtime.Serialization;

namespace Fetcho.Common
{
    [Serializable]
    public class FetchoResourceBlockedException : Exception
    {
        public FetchoResourceBlockedException()
        {
        }

        public FetchoResourceBlockedException(string blockReason) : base(string.Format("URI is blocked, {0}", blockReason))
        {
        }

        public FetchoResourceBlockedException(string blockReason, Exception innerException) : base(blockReason, innerException)
        {
        }

        protected FetchoResourceBlockedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
