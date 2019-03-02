using System;
using System.IO;
using System.Threading.Tasks;

namespace Fetcho.Common
{
    public interface IWebDataPacketConsumer
    {
        string Name { get;  }

        bool ProcessesRequest { get;  }
        bool ProcessesResponse { get; }
        bool ProcessesException { get; }

        /// <summary>
        /// Called each time a new resource is loaded
        /// </summary>
        void NewResource();

        /// <summary>
        /// When the file starts being processed
        /// </summary>
        void PacketOpened();
        void ProcessRequest(string request);
        void ProcessResponseHeaders(string responseHeaders);
        void ProcessResponseStream(Stream dataStream);
        void ProcessException(string exception);

        /// <summary>
        /// If something goes wrong reading the file
        /// </summary>
        /// <param name="ex"></param>
        void ReadingException(Exception ex);

        /// <summary>
        /// Once the file is complete
        /// </summary>
        void PacketClosed();
    }
}