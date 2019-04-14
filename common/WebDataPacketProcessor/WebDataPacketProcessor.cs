using log4net;
using System;
using System.Threading.Tasks;

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

        public async Task Process(WebDataPacketReader packet)
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
                            await Consumer.ProcessRequest(requestString).ConfigureAwait(false);
                        }

                        if (Consumer.ProcessesResponse)
                        {
                            string responseHeaders = packet.GetResponseHeaders();
                            await Consumer.ProcessResponseHeaders(responseHeaders).ConfigureAwait(false);

                            using (var response = packet.GetResponseStream())
                            {
                                await Consumer.ProcessResponseStream(response).ConfigureAwait(false);
                            }
                        }

                        if (Consumer.ProcessesException)
                        {
                            string exception = packet.GetException();
                            await Consumer.ProcessException(exception).ConfigureAwait(false);
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
