using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Xml;

namespace Fetcho.Common
{

    public class WebDataPacketWriter : IDisposable, IWebResourceWriter
    {
        public XmlWriter Writer { get; private set; }

        public int ResourcesWritten { get; private set; }

        public string FileName { get; set; }

        public WebDataPacketWriter(string fileName)
        {
            FileName = fileName;
            StartPacket(new StreamWriter(OpenFile(FileName)));
        }

        public WebDataPacketWriter(Stream stream)
        {
            StartPacket(new StreamWriter(stream));
        }

        private void StartPacket(TextWriter writer)
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
            Utility.LogInfo("New packet: {0}", fileName);
            return new GZipStream(new FileStream(fileName, FileMode.Open, FileAccess.Write, FileShare.Read),
                                  CompressionLevel.Optimal, false);
        }

        private void WriteStartOfFile()
        {
            Writer.WriteStartDocument();
            Writer.WriteStartElement("resources");
            Writer.WriteStartElement("startTime");
            Writer.WriteValue(DateTime.UtcNow);
            Writer.WriteEndElement();
        }

        public void OutputStartResource(QueueItem item)
        {
            Writer.WriteStartElement("resource");
        }

        public void OutputRequest(WebRequest request, DateTime startTime)
        {
            if (request == null)
            {
                Writer.WriteStartElement("request");
                Writer.WriteEndElement();
            }
            else
            {
                DateTime now = DateTime.UtcNow;
                Writer.WriteStartElement("request");
                Writer.WriteString(string.Format("Uri: {0}\n", request.RequestUri == null ? "" : request.RequestUri.ToString().CleanupForXml()));
                Writer.WriteString(string.Format("ResponseTime: {0}\n", now - startTime));
                Writer.WriteString(string.Format("Date: {0}\n", now));
                // AllKeys is slower than Keys but is a COPY to prevent errors from updates to the collection
                if (request.Headers != null)
                {
                    foreach (string key in request.Headers.AllKeys)
                    {
                        Writer.WriteString(string.Format("{0}: {1}\n", key, request.Headers[key].CleanupForXml()));
                    }
                }
                Writer.WriteEndElement();
            }
        }

        public void OutputResponse(WebResponse response, byte[] buffer, int bytesRead)
        {
            Writer.WriteStartElement("response");
            Writer.WriteStartElement("header");

            try
            {
                if (response is HttpWebResponse httpWebResponse)
                {
                    Writer.WriteString(string.Format("status: {0} {1}\n", httpWebResponse.StatusCode, httpWebResponse.StatusDescription));
                }

                foreach (string key in response.Headers)
                {
                    Writer.WriteString(string.Format("{0}: {1}\n", key, response.Headers[key]));
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Writer.WriteEndElement(); // header
            }

            try
            {

                Writer.WriteStartElement("data");
                Writer.WriteBase64(buffer, 0, bytesRead);

            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Writer.WriteEndElement(); // data
                Writer.WriteEndElement(); // response
            }
        }

        public void OutputException(Exception ex)
        {
            if (ex == null) return;
            Writer.WriteElementString("exception", ex.ToString().CleanupForXml());
        }

        public void OutputEndResource()
        {
            Writer.WriteEndElement(); // resource

            if (++ResourcesWritten % 10000 == 0)
                Writer.Flush();
            ReplaceDataPacketWriterIfQuotaReached();
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

        private void EndPacket()
        {
            WriteEndOfFile();
            Writer.Dispose();
            Writer = null;
        }

        private void ReplaceDataPacketWriterIfQuotaReached()
        {
            if (ResourcesWritten > FetchoConfiguration.Current.MaxResourcesPerDataPacket)
            {
                EndPacket();
                StartPacket(new StreamWriter(OpenFile(FileName)));
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    EndPacket();
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
