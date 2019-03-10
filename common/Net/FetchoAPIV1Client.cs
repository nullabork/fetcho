using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Fetcho.Common.Entities;

namespace Fetcho.Common.Net
{
    public class FetchoAPIV1Client : IDisposable
    {
        private const string BaseEndpoint = "api/v1";
        private const string AccessKeysEndPoint = BaseEndpoint + "/accesskeys";
        private const string WorkspaceEndPoint = BaseEndpoint + "/workspaces";
        private const string JsonContentType = "application/json";

        private HttpClient client = new HttpClient();

        public FetchoAPIV1Client(Uri baseAddress)
        {
            client = new HttpClient();
            client.BaseAddress = baseAddress;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(JsonContentType));
        }

        public async Task PostResultsByWorkspaceAsync(Guid workspaceId, IEnumerable<WorkspaceResult> results)
        {
            string path = String.Format("{0}/{1}/results", WorkspaceEndPoint, workspaceId);
            var response = await client.PostAsJsonAsync(path, results);
            response.EnsureSuccessStatusCode();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    client.Dispose();
                }

                client = null;
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
