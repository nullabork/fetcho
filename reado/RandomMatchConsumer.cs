using Fetcho.Common;
using Fetcho.Common.Entities;
using Fetcho.FetchoAPI.Controllers;
using System;
using System.IO;
using System.Text;
using System.Web;

namespace Fetcho
{
    /// <summary>
    /// Used to find links that match specific queries
    /// </summary>
    internal class RandomMatchConsumer : IWebDataPacketConsumer
    {
        public Uri CurrentUri;

        public string Name { get => "Extract Links that match"; }
        public bool ProcessesRequest { get => true; }
        public bool ProcessesResponse { get => true; }
        public bool ProcessesException { get => false; }

        int jumpTo = 0;
        int count = 30;
        WorkspacesController controller = null;
        Random random = new Random(DateTime.Now.Millisecond);

        public RandomMatchConsumer(params string[] args)
        {
            jumpTo = random.Next(0, 50000);
            Utility.LogInfo("JumpTo: {0}", jumpTo);
            controller = new WorkspacesController();
        }

        public void ProcessException(string exception)
        {
        }

        public void ProcessRequest(string request) => CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);

        public void ProcessResponseHeaders(string responseHeaders)
        {
        }

        public async void ProcessResponseStream(Stream dataStream)
        {
            try
            {
                if (dataStream == null) return;
                if (jumpTo-- > 0) return;
                if (count <= 0) return;

                Guid workspaceId = new Guid("f5201ff7-ea59-4e00-87b9-af4a0a9c8e2e");
                var result = ReadNextWebResource(new StreamReader(dataStream));
                if (!AddToTheList(result)) return;
                await controller.PostResultsByWorkspace(workspaceId, new WorkspaceResult[] { result });
                count--;
            }
            catch( Exception ex)
            {
                Utility.LogException(ex);
            }
        }

        public void NewResource()
        {
            CurrentUri = null;
        }

        public void PacketClosed()
        {

        }

        public void PacketOpened()
        {

        }
        public void ReadingException(Exception ex) { }

        bool AddToTheList(WorkspaceResult result) =>
            result != null &&
            !String.IsNullOrWhiteSpace(result.Title);

        WorkspaceResult ReadNextWebResource(TextReader reader)
        {
            var r = new WorkspaceResult
            {
                Hash = MD5Hash.Compute(CurrentUri).ToString(),
                ReferrerUri = "",
                Uri = CurrentUri.ToString(),
                Title = "",
                Description = "",
                Created = DateTime.Now,
                PageSize = 0
            };

            string line = reader.ReadLine();
            if (line == null)
                return r;
            else
            {

                r.PageSize += line.Length;
                while (reader.Peek() > 0)
                {
                    if (String.IsNullOrWhiteSpace(r.Title) && line.ToLower().Contains("<title")) r.Title = ReadTitle(line);
                    else if (String.IsNullOrWhiteSpace(r.Description) && line.ToLower().Contains("description")) r.Description = ReadDesc(line).Truncate(100);
                    line = reader.ReadLine();
                    r.PageSize += line.Length;
                }

                return r;
            }
        }

        string ReadDesc(string line)
        {
            const string StartPoint = "content=\"";
            var sb = new StringBuilder();

            int start = line.ToLower().IndexOf(StartPoint);
            if (start == -1) return "";
            start += StartPoint.Length - 1;
            while (++start < line.Length && line[start] != '"') // </meta >
                sb.Append(line[start]);

            return HttpUtility.HtmlDecode(sb.ToString().Trim());
        }


        string ReadTitle(string line)
        {
            var sb = new StringBuilder();

            int start = line.ToLower().IndexOf("<title");
            while (++start < line.Length && line[start] != '>') ;
            while (++start < line.Length && line[start] != '<') // </title>
                sb.Append(line[start]);

            return HttpUtility.HtmlDecode(sb.ToString().Trim());
        }

        string ReadUri(string line)
        {
            if (line.Length < 5) return "";
            return line.Substring(5).Trim();
        }
    }


}
