using Fetcho.Common;
using Fetcho.Common.Entities;
using Fetcho.Common.QueryEngine;
using Fetcho.FetchoAPI.Controllers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web;

namespace Fetcho
{
    /// <summary>
    /// Used to find links that match specific queries
    /// </summary>
    internal class QueryConsumer : IWebDataPacketConsumer
    {
        List<WorkspaceQuery> Queries { get; }

        public Uri CurrentUri;

        public string Name { get => "Extract Links that match"; }
        public bool ProcessesRequest { get => true; }
        public bool ProcessesResponse { get => true; }
        public bool ProcessesException { get => false; }

        WorkspacesController controller = null;

        public QueryConsumer(params string[] args)
        {
            controller = new WorkspacesController();
            Queries = new List<WorkspaceQuery>();
        }

        public void ProcessException(string exception)
        {
        }

        public void ProcessRequest(string request) => CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);

        public void ProcessResponseHeaders(string responseHeaders)
        {
        }

        public void ProcessResponseStream(Stream dataStream)
        {
            try
            {
                if (dataStream == null) return;

                using (var reader = new StreamReader(dataStream))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        foreach (var wq in Queries)
                        {
                            var r = wq.Query.Evaluate(CurrentUri, line);
                            if (r.Action == EvaluationResultAction.Include)
                            {
                                var result = ReadNextWebResource(new StreamReader(dataStream));
                                if (AddToTheWorkspace(result))
                                    controller.PostResultsByWorkspace(wq.WorkspaceId, new WorkspaceResult[] { result }).GetAwaiter().GetResult();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
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
            using (var db = new Database())
            {
                db.UpdateWorkspaceStatistics().GetAwaiter().GetResult();
                var workspaces = db.GetWorkspaces().GetAwaiter().GetResult();
                foreach (var workspace in workspaces)
                    if (!String.IsNullOrWhiteSpace(workspace.QueryText) && workspace.IsActive)
                        Queries.Add(new WorkspaceQuery { Query = new Query(workspace.QueryText), WorkspaceId = workspace.WorkspaceId });
            }
        }

        public void ReadingException(Exception ex) { }

        bool AddToTheWorkspace(WorkspaceResult result) =>
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

        private struct WorkspaceQuery
        {
            public Guid WorkspaceId;
            public Query Query;
        }
    }


}
