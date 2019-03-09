using Fetcho.Common;
using Fetcho.Common.Entities;
using Fetcho.Common.QueryEngine;
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
    internal class QueryConsumer : IWebDataPacketConsumer, IWebResource
    {
        List<WorkspaceQuery> Queries { get; }

        private List<Guid> postTo = new List<Guid>();
        private WorkspaceResult result = null;
        private StringBuilder evaluationText = new StringBuilder();
        private string requestString = String.Empty;
        private string responseHeaders = String.Empty;
        private Dictionary<string, string> requestProperties = new Dictionary<string, string>();
        private Dictionary<string, string> responseProperties = new Dictionary<string, string>();
        private bool _processed = false;

        public Dictionary<string, string> RequestProperties { get { if (!_processed) Process(); return requestProperties; } }
        public Dictionary<string, string> ResponseProperties { get { if (!_processed) Process(); return responseProperties; } }
        public Dictionary<string, object> PropertyCache { get; private set; }

        public string Name { get => "Processes workspace queries"; }
        public bool ProcessesRequest { get => true; }
        public bool ProcessesResponse { get => true; }
        public bool ProcessesException { get => false; }

        public QueryConsumer(params string[] args)
        {
            Queries = new List<WorkspaceQuery>();
            ClearAll();
        }

        public void ProcessException(string exception) { }

        public void ProcessRequest(string request) => this.requestString = request;

        public void ProcessResponseHeaders(string responseHeaders) => this.responseHeaders = responseHeaders;

        public void ProcessResponseStream(Stream dataStream)
        {
            try
            {
                if (dataStream == null) return;

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
                        {
                            string title = ReadTitle(reader);
                            if (!PropertyCache.ContainsKey("title"))
                                PropertyCache.Add("title", title);
                            PropertyCache["title"] = title;
                        }

                        if (node.Value == "meta")
                        {
                            var desc = ReadDesc(reader);
                            if (!String.IsNullOrWhiteSpace(desc))
                            {
                                if (!PropertyCache.ContainsKey("description"))
                                    PropertyCache.Add("description", desc);
                                PropertyCache["description"] = desc;
                            }
                        }
                    }
                }

                result = new WorkspaceResult
                {
                    Hash = MD5Hash.Compute(RequestProperties["uri"]).ToString(),
                    RefererUri = RequestProperties.SafeGet("referer"),
                    Uri = RequestProperties["uri"],
                    Title = PropertyCache.SafeGet("title")?.ToString(),
                    Description = PropertyCache.SafeGet("description")?.ToString(),
                    Created = DateTime.Now,
                    PageSize = 0
                };


                foreach (var wq in Queries)
                {
                    try
                    {
                        var r = wq.Query.Evaluate(this, evaluationText.ToString());
                        if (r.Action == EvaluationResultAction.Include)
                        {
                            result.Tags.AddRange(r.Tags);
                            postTo.Add(wq.WorkspaceId);
                        }
                    }
                    catch (Exception ex)
                    {
                        Utility.LogException(ex);
                    }
                }

                if (!postTo.Any()) return;

                foreach (var workspaceId in postTo.ToArray())
                {
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
            => ClearAll();

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

        string ReadDesc(HtmlReader reader)
        {
            if (reader.GetAttribute("name").Contains("description"))
                return reader.GetAttribute("content").Trim().Replace("\n", " ").Replace("\t", " ").Replace("\r", " ");
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

            return title.Trim().Replace("\n", " ").Replace("\t", " ").Replace("\r", " ");
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

        void ClearAll()
        {
            postTo.Clear();
            evaluationText.Clear();
            requestProperties = null;
            responseProperties = null;
            PropertyCache = new Dictionary<string, object>();
            requestString = String.Empty;
            responseHeaders = String.Empty;
            _processed = false;
        }

        void Process()
        {
            _processed = true;
            requestProperties = new Dictionary<string, string>();
            responseProperties = new Dictionary<string, string>();

            if (!String.IsNullOrWhiteSpace(requestString))
            {
                var lines = requestString.Split('\n');
                foreach (var line in lines)
                {
                    int idx = line.IndexOf(':');
                    if (idx < 0) continue;
                    string key = line.Substring(0, idx).Trim().ToLower();
                    string value = line.Substring(idx + 1).Trim();

                    if (!String.IsNullOrWhiteSpace(key) && !requestProperties.ContainsKey(key))
                        requestProperties.Add(key, value);
                }
            }

            if (!String.IsNullOrWhiteSpace(responseHeaders))
            {
                var lines = responseHeaders.Split('\n');
                foreach (var line in lines)
                {
                    int idx = line.IndexOf(':');
                    if (idx < 0) continue;
                    string key = line.Substring(0, idx).Trim().ToLower();
                    string value = line.Substring(idx + 1).Trim();

                    if (!String.IsNullOrWhiteSpace(key) && !responseProperties.ContainsKey(key))
                        responseProperties.Add(key, value);
                }
            }
        }

        private struct WorkspaceQuery
        {
            public Guid WorkspaceId;
            public Query Query;
        }
    }


}
