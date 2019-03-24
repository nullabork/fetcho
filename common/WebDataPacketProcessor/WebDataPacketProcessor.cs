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

        public WebDataPacketConsumer Consumer { get; set; }

        public long ResourcesProcessedCount { get; set; }

        public WebDataPacketProcessor()
        {
        }

        public void Process(WebDataPacketReader packet)
        {

            try
            {
                Consumer.PacketOpened();

                do
                {
                    try
                    {
                        Consumer.NewResource();

                        if (Consumer.ProcessesRequest)
                        {
                            string requestString = packet.GetRequestString();
                            Consumer.ProcessRequest(requestString);
                        }

                        if (Consumer.ProcessesResponse)
                        {
                            string responseHeaders = packet.GetResponseHeaders();
                            Consumer.ProcessResponseHeaders(responseHeaders);

                            using (var response = packet.GetResponseStream())
                            {
                                Consumer.ProcessResponseStream(response);
                            }
                        }

                        if (Consumer.ProcessesException)
                        {
                            string exception = packet.GetException();
                            Consumer.ProcessException(exception);
                        }

                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                    finally
                    {
                    }

                    ResourcesProcessedCount++;

                    if (packet.ResourceCountSeen > WebDataPacketReader.MaxResourcesInAFile)
                        throw new FetchoException("Something wrong with packet - it keeps spinning");
                }
                while (packet.NextResource());
            }
            catch (Exception ex)
            {
                Consumer.ReadingException(ex);
            }
            finally
            {
                Consumer.PacketClosed();
            }
        }
    }
}
