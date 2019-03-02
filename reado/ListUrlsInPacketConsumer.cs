using Fetcho.Common;
using log4net;
using System;
using System.IO;
using System.Threading.Tasks;

namespace reado
{
    public class ListUrisInPacketConsumer : IWebDataPacketConsumer
    {
        static readonly ILog log = LogManager.GetLogger(typeof(ListUrisInPacketConsumer));

        public Uri CurrentUri;
        public ContentType ContentType;

        public string Name { get => "List URIs"; }
        public bool ProcessesRequest { get => true; }
        public bool ProcessesResponse { get => false; }
        public bool ProcessesException { get => false; }

        public void ProcessException(string exception)
        {
        }

        public void ProcessRequest(string request)
        {
            CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);
            Console.WriteLine(CurrentUri);
        }

        public void ProcessResponseHeaders(string responseHeaders)
        {
        }

        public void ProcessResponseStream(Stream dataStream)
        {
        }

        public void NewResource()
        {
            CurrentUri = null;
            ContentType = null;
        }

        public void PacketClosed()
        {

        }

        public void PacketOpened()
        {

        }

        public void ReadingException(Exception ex) { }
    }
}
