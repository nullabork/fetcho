using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Fetcho.ContentReaders;

namespace Fetcho.Common
{
    /// <summary>
    /// Writes resources straight to workspaces as well as to file using WebDataPacketWriter
    /// </summary>
    public class WorkspaceResourcePacketWriter : IDisposable, IWebResourceWriter
    {
        public WebDataPacketWriter Writer { get; set; }

        private string requestString = String.Empty;
        private string responseHeaders = String.Empty;
        private QueueItem queueItem = null;

        public WorkspaceResourcePacketWriter(WebDataPacketWriter writer)
        {
            Writer = writer;
        }

        public void OutputStartResource(QueueItem item)
        {
            queueItem = item;
            requestString = String.Empty;
            responseHeaders = String.Empty;
            Writer.OutputStartResource(item);
        }

        public void OutputRequest(WebRequest request, DateTime startTime)
        {
            Writer.OutputRequest(request, startTime);

            if (queueItem is ImmediateWorkspaceQueueItem wqi)
            {
                DateTime now = DateTime.UtcNow;
                var sb = new StringBuilder();
                sb.AppendFormat("Uri: {0}\n", request.RequestUri == null ? "" : request.RequestUri.ToString().CleanupForXml());
                sb.AppendFormat("ResponseTime: {0}\n", now - startTime);
                sb.AppendFormat("Date: {0}\n", now);
                // AllKeys is slower than Keys but is a COPY to prevent errors from updates to the collection
                if (request.Headers != null)
                {
                    foreach (string key in request.Headers.AllKeys)
                    {
                        sb.AppendFormat("{0}: {1}\n", key, request.Headers[key].CleanupForXml());
                    }
                }
                requestString = sb.ToString();
            }
        }

        public async void OutputResponse(WebResponse response, byte[] buffer, int bytesRead)
        {
            try
            {

                Writer.OutputResponse(response, buffer, bytesRead);

                // bail if we dont get anything
                if (bytesRead == 0) return;

                // if we need to push it to this workspace
                if (queueItem is ImmediateWorkspaceQueueItem wqi)
                {
                    try
                    {
                        var sb = new StringBuilder();

                        if (response is HttpWebResponse httpWebResponse)
                        {
                            sb.AppendFormat("status: {0} {1}\n", httpWebResponse.StatusCode, httpWebResponse.StatusDescription);
                        }

                        foreach (string key in response.Headers)
                        {
                            sb.AppendFormat("{0}: {1}\n", key, response.Headers[key]);
                        }

                        responseHeaders = sb.ToString();

                        using (var ms = new MemoryStream(buffer))
                        {
                            var builder = new WorkspaceResultBuilder();
                            var result = builder.Build(ms, requestString, responseHeaders, out string evalText);
                            result.Tags.AddRange(wqi.Tags);

                            var hash = MD5Hash.Compute(buffer);
                            var db = await DatabasePool.GetDatabaseAsync();
                            try
                            {
                                await db.AddWorkspaceResults(wqi.DestinationWorkspaceId, new[] { result });
                                // OPTIMISE: Remove ToArray and just pass IEnumerable<> 
                                await db.AddWebResourceDataCache(hash, buffer.Take(bytesRead).ToArray());
                            }
                            catch (Exception ex)
                            {
                                Utility.LogException(ex);
                            }
                            finally
                            {
                                await DatabasePool.GiveBackToPool(db);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Utility.LogException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
        }

        public void OutputException(Exception ex)
        {
            Writer.OutputException(ex);
        }

        public void OutputEndResource()
        {
            Writer.OutputEndResource();
        }

        public void Dispose()
        {
            Writer.Dispose();
        }
    }
}
