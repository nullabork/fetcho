using System;
using System.Net;

namespace Fetcho.Common
{
    public interface IWebResourceWriter : IDisposable
    {
        void OutputStartResource(QueueItem item);
        void OutputRequest(WebRequest request, DateTime startTime);
        void OutputResponse(WebResponse response, byte[] buffer, int bytesRead);
        void OutputException(Exception ex);
        void OutputEndResource();
    }
}
