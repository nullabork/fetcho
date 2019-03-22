using System;
using System.IO;
using System.Xml;

namespace Fetcho.Common
{
    public class WebDataPacketWriter : IDisposable
    {
        public XmlWriter Writer { get; private set; }

        public int ResourcesWritten { get; private set; }

        public WebDataPacketWriter(string fileName) : this(OpenFile(fileName))
        {
        }

        public WebDataPacketWriter(Stream stream) : this(new StreamWriter(stream))
        {
        }

        public WebDataPacketWriter(TextWriter writer) 
        {
            ResourcesWritten = 0;
            Writer = GetWriter(writer);
            WriteStartOfFile();
        }

        private XmlWriter GetWriter(TextWriter sw)
        {
            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.NewLineHandling = NewLineHandling.Replace;
            return XmlWriter.Create(sw, settings);
        }

        /// <summary>
        /// Open the XmlWriter we're going to write to
        /// </summary>
        /// <returns></returns>
        private static Stream OpenFile(string fileName)
        {
            fileName = Utility.CreateNewFileOrIndexNameIfExists(fileName);
            return new FileStream(fileName, FileMode.Open, FileAccess.Write, FileShare.Read);
        }

        private void WriteStartOfFile()
        {
            Writer.WriteStartDocument();
            Writer.WriteStartElement("resources");
            Writer.WriteStartElement("startTime");
            Writer.WriteValue(DateTime.UtcNow);
            Writer.WriteEndElement();
        }

        public void OutputStartResource()
        {
            Writer.WriteStartElement("resource");
        }

        public void OutputEndResource()
        {
            Writer.WriteEndElement(); // resource

            if ( ResourcesWritten++ % 1000 == 0)
                Writer.Flush();
        }
        
        private void WriteEndOfFile()
        {
            Writer.WriteStartElement("endTime");
            Writer.WriteValue(DateTime.UtcNow);
            Writer.WriteEndElement(); // endtime
            Writer.WriteEndElement(); // resources
            Writer.WriteEndDocument();
            Writer.Flush();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    WriteEndOfFile();
                    Writer.Dispose();
                    Writer = null;
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion


    }
}
