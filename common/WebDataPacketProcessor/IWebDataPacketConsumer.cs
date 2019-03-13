using System;
using System.IO;

namespace Fetcho.Common
{
    public abstract class WebDataPacketConsumer
    {
        public virtual string Name { get;  }

        public virtual bool ProcessesRequest { get;  }
        public virtual bool ProcessesResponse { get; }
        public virtual bool ProcessesException { get; }

        /// <summary>
        /// Called each time a new resource is loaded
        /// </summary>
        public virtual void NewResource() { }

        /// <summary>
        /// When the file starts being processed
        /// </summary>
        public virtual void PacketOpened() { }
        public virtual void ProcessRequest(string request) { }
        public virtual void ProcessResponseHeaders(string responseHeaders) { }
        public virtual void ProcessResponseStream(Stream dataStream) { }
        public virtual void ProcessException(string exception) { }

        /// <summary>
        /// If something goes wrong reading the file
        /// </summary>
        /// <param name="ex"></param>
        public virtual void ReadingException(Exception ex) { }

        /// <summary>
        /// Once the file is complete
        /// </summary>
        public virtual void PacketClosed() { }
    }
}