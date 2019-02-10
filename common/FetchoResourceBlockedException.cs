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

        public FetchoResourceBlockedException(string block_reason) : base(string.Format("URI is blocked, {0}", block_reason))
        {
        }

        public FetchoResourceBlockedException(string block_reason, Exception innerException) : base(block_reason, innerException)
        {
        }

        protected FetchoResourceBlockedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
