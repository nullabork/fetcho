using System;
using System.Runtime.Serialization;

namespace Fetcho.Common
{
    /// <summary>
    /// Description of FetchoException.
    /// </summary>
    [Serializable]
  public class FetchoException : Exception
  {
    public FetchoException()
    {
    }
    
    public FetchoException(string message) : base(message)
    {
    }
    
    public FetchoException(string message, Exception innerException) : base(message, innerException)
    {
    }
    
    protected FetchoException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
  }
}
