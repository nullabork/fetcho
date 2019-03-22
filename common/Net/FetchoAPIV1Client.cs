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
        private const int DefaultResultsCount = 50;
        private const int DefaultRandomResultsCount = 1;
        private const string BaseEndpoint = "api/v1";
        private const string AccessKeysEndPoint = BaseEndpoint + "/accesskeys";
        private const string AccessKeyEndPoint = BaseEndpoint + "/accesskey";
        private const string AccountsEndPoint = BaseEndpoint + "/accounts";
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

        #region workspaces

        public async Task PostWorkspaceResultsByWorkspaceIdAsync(Guid workspaceId, IEnumerable<WorkspaceResult> results)
        {
            string path = String.Format("{0}/{1}/results", WorkspaceEndPoint, workspaceId);
            var response = await client.PostAsJsonAsync(path, results);
            response.EnsureSuccessStatusCode();
        }

        public async Task<WorkspaceResult[]> GetRandomWorkspaceResultsAsync(Guid workspaceId, int count = DefaultRandomResultsCount)
        {
            string path = String.Format("{0}/{1}/results/random?count={2}", WorkspaceEndPoint, workspaceId, count);
            var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<WorkspaceResult[]>();
        }

        public async Task<WorkspaceResult[]> GetWorkspaceResultsAsync(Guid workspaceId, int fromSequence = 0, int count = DefaultResultsCount)
        {
            string path = String.Format("{0}/{1}/results?fromSequence={2}&count={3}", WorkspaceEndPoint, workspaceId, fromSequence, count);
            var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<WorkspaceResult[]>();
        }

        #endregion

        #region access keys

        public async Task<AccessKey> GetAccessKeyAsync(Guid accessKeyId)
        {
            string path = String.Format("{0}/{1}", AccessKeyEndPoint, accessKeyId);
            var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<AccessKey>();
        }

        public async Task<WorkspaceResult[]> GetWorkspaceResultsByAccessKey(Guid accessKeyId, int fromSequence = 0, int count = DefaultResultsCount)
        {
            string path = String.Format("{0}/{1}/results?fromSequence={2}&count={3}", AccessKeyEndPoint, accessKeyId, fromSequence, count);
            var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<WorkspaceResult[]>();
        }

        public async Task PostWorkspaceResultsByAccessKey(Guid accessKeyId, IEnumerable<WorkspaceResult> results)
        {
            string path = String.Format("{0}/{1}/results", AccessKeyEndPoint, accessKeyId);
            var response = await client.PostAsJsonAsync(path, results);
            response.EnsureSuccessStatusCode();
        }

        public async Task<WorkspaceResult[]> GetRandomWorkspaceResultsByAccessKey(Guid accessKeyId, int count = DefaultRandomResultsCount)
        {
            string path = String.Format("{0}/{1}/results/random?count={2}", AccessKeyEndPoint, accessKeyId, count);
            var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<WorkspaceResult[]>();
        }

        public async Task<AccessKey> PostAccessKeyAsync(AccessKey accessKey)
        {
            var response = await client.PostAsJsonAsync(AccessKeysEndPoint, accessKey);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<AccessKey>();
        }

        public async Task<AccessKey> PutAccessKeyAsync(AccessKey accessKey)
        {
            var response = await client.PutAsJsonAsync(AccessKeysEndPoint, accessKey);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<AccessKey>();
        }

        public async Task DeleteAccessKeyAsync(AccessKey accessKey)
        {
            string path = String.Format("{0}/{1}", AccessKeyEndPoint, accessKey.Id);
            var response = await client.DeleteAsync(path);
            response.EnsureSuccessStatusCode();
        }

        #endregion

        #region accounts

        public async Task<Account> PostAccountAsync(Account account)
        {
            var response = await client.PostAsJsonAsync(AccountsEndPoint, account);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<Account>();
        }

        public async Task<Account> PutAccountAsync(Account account)
        {
            var response = await client.PutAsJsonAsync(AccountsEndPoint, account);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<Account>();
        }

        public async Task DeleteAccountAsync(Account account)
        {
            string path = String.Format("{0}/{1}", AccountsEndPoint, account.Name);
            var response = await client.DeleteAsync(path);
            response.EnsureSuccessStatusCode();
        }

        public async Task<Account> GetAccountAsync(string accountName)
        {
            string path = String.Format("{0}/{1}", AccountsEndPoint, accountName);
            var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<Account>();
        }

        #endregion

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
