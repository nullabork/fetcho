using log4net;
using System;

namespace Fetcho.Common
{
    /// <summary>
    /// Base class for processing packets in a systematic way
    /// </summary>
    public class WebDataPacketProcessor
    {
        public static readonly ILog log = LogManager.GetLogger(typeof(WebDataPacketProcessor));

        public IWebDataPacketConsumer Consumer { get; set; }

        public int ResourcesProcessedCount { get; set; }

        public WebDataPacketProcessor()
        {
        }

        public void Process(WebDataPacketReader packet)
        {
            Consumer.PacketOpened();
            do
            {
                try
                {
                    if ( Consumer.ProcessesRequest )
                    {
                        string requestString = packet.GetRequestString();
                        Consumer.ProcessRequest(requestString);
                    }

                    if (Consumer.ProcessesResponse)
                    {
                        string responseHeaders = packet.GetResponseHeaders();
                        Consumer.ProcessResponseHeaders(responseHeaders);

                        var response = packet.GetResponseStream();
                        Consumer.ProcessResponseStream(response);
                    }

                    if (Consumer.ProcessesException)
                    {
                        string exception = packet.GetException();
                        Consumer.ProcessException(exception);
                    }

                    Consumer.NewResource();
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }

                ResourcesProcessedCount++;
            }
            while (packet.NextResource());
            Consumer.PacketClosed();
        }
    }
}
