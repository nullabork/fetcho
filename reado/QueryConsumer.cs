using Fetcho.Common;
using Fetcho.Common.Entities;
using Fetcho.Common.QueryEngine;
using Fetcho.FetchoAPI.Controllers;
using System;
using System.Collections.Generic;
using System.IO;
using BracketPipe;
using System.Linq;
using System.Text;

namespace Fetcho
{
    /// <summary>
    /// Used to find links that match specific queries
    /// </summary>
    internal class QueryConsumer : IWebDataPacketConsumer
    {
        List<WorkspaceQuery> Queries { get; }

        public Uri CurrentUri;
        public Uri RefererUri;

        public string Name { get => "Processes workspace queries"; }
        public bool ProcessesRequest { get => true; }
        public bool ProcessesResponse { get => true; }
        public bool ProcessesException { get => false; }

        public QueryConsumer(params string[] args)
        {
            Queries = new List<WorkspaceQuery>();
        }

        public void ProcessException(string exception) { }

        public void ProcessRequest(string request)
        {
            CurrentUri = WebDataPacketReader.GetUriFromRequestString(request);
            RefererUri = WebDataPacketReader.GetRefererUriFromRequestString(request);
        }

        public void ProcessResponseHeaders(string responseHeaders)
        {
        }

        private List<Guid> postTo = new List<Guid>();
        private WorkspaceResult result = null;
        private StringBuilder evaluationText = new StringBuilder();

        public void ProcessResponseStream(Stream dataStream)
        {
            try
            {
                postTo.Clear();
                evaluationText.Clear();
                if (dataStream == null) return;

                result = new WorkspaceResult
                {
                    Hash = MD5Hash.Compute(CurrentUri).ToString(),
                    RefererUri = RefererUri?.ToString(),
                    Uri = CurrentUri.ToString(),
                    Title = "",
                    Description = "",
                    Created = DateTime.Now,
                    PageSize = 0
                };


                using (var reader = new HtmlReader(dataStream))
                {
                    while (!reader.EOF)
                    {
                        var node = reader.NextNode();
                        if (node.Type == HtmlTokenType.Text)
                            Evaluate(node.Value);

                        if (node.Value == "script")
                            ReadToEndTag(reader, "script");

                        if (node.Value == "style")
                            ReadToEndTag(reader, "style");

                        if (node.Value == "title")
                            result.Title = ReadTitle(reader);

                        if (node.Value == "meta")
                        {
                            var desc = ReadDesc(reader);
                            if (!String.IsNullOrWhiteSpace(desc))
                                result.Description = desc;
                        }
                    }
                }

                foreach (var wq in Queries)
                {
                    try
                    {
                        var r = wq.Query.Evaluate(CurrentUri, evaluationText.ToString());
                        if (r.Action == EvaluationResultAction.Include)
                        {
                            result.Tags.AddRange(r.Tags);
                            postTo.Add(wq.WorkspaceId);
                        }
                    }
                    catch(Exception ex)
                    {
                        Utility.LogException(ex);
                    }
                }

                foreach (var workspaceId in postTo.ToArray())
                {
                    if (AddToTheWorkspace(result))
                        using (var db = new Database())
                            db.AddWorkspaceResults(workspaceId, new WorkspaceResult[] { result }).GetAwaiter().GetResult();
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
            RefererUri = null;
        }

        public void PacketClosed() { }

        public void PacketOpened()
        {
            using (var db = new Database())
            {
                UpdateStatistics(db);
                SetupQueries(db);
            }
        }

        public void ReadingException(Exception ex) { }

        void Evaluate(string fragment)
        {
            evaluationText.Append(fragment);
            evaluationText.Append(' ');
        }

        bool AddToTheWorkspace(WorkspaceResult result) =>
            result != null &&
            !String.IsNullOrWhiteSpace(result.Title);


        string ReadDesc(HtmlReader reader)
        {
            if (reader.GetAttribute("name").Contains("description"))
                return reader.GetAttribute("content");
            return String.Empty;
        }

        string ReadTitle(HtmlReader reader)
        {
            string title = "";

            while (!reader.EOF)
            {
                var node = reader.NextNode();

                if (node.Type == HtmlTokenType.Text)
                    title += node.Value;

                if (node.Type == HtmlTokenType.EndTag && node.Value == "title")
                    return title;
            }

            return title;
        }

        void ReadToEndTag(HtmlReader reader, string endTag)
        {
            while (!reader.EOF)
            {
                var n = reader.NextNode();

                if (n.Value == endTag && n.Type == HtmlTokenType.EndTag)
                    return;
            }
        }

        void UpdateStatistics(Database db)
            => db.UpdateWorkspaceStatistics().GetAwaiter().GetResult();

        void SetupQueries(Database db)
        {
            Queries.Clear();
            var workspaces = db.GetWorkspaces().GetAwaiter().GetResult();
            foreach (var workspace in workspaces.Distinct())
                if (!String.IsNullOrWhiteSpace(workspace.QueryText) && workspace.IsActive)
                    Queries.Add(new WorkspaceQuery { Query = new Query(workspace.QueryText), WorkspaceId = workspace.WorkspaceId });
        }

        private struct WorkspaceQuery
        {
            public Guid WorkspaceId;
            public Query Query;
        }
    }


}
