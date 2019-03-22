using Fetcho.Common;
using Fetcho.Common.Entities;
using Fetcho.Common.QueryEngine;
using System;
using System.Collections.Generic;
using System.IO;
using BracketPipe;
using System.Linq;
using System.Text;
using Fetcho.Common.Net;

namespace Fetcho
{
    /// <summary>
    /// Used to find links that match specific queries
    /// </summary>
    internal class QueryConsumer : WebDataPacketConsumer, IWebResource
    {
        Dictionary<Guid, Query> Queries { get; }

        private FetchoAPIV1Client fetchoClient;
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

        public override string Name { get => "Processes workspace queries"; }
        public override bool ProcessesRequest { get => true; }
        public override bool ProcessesResponse { get => true; }

        public int ResourcesSeen = 0;

        public QueryConsumer(params string[] args)
        {

            fetchoClient = new FetchoAPIV1Client(new Uri(FetchoConfiguration.Current.FetchoWorkspaceServerBaseUri));
            Queries = new Dictionary<Guid, Query>();
            ClearAll();
        }

        public override void ProcessRequest(string request)
            => this.requestString = request;

        public override void ProcessResponseHeaders(string responseHeaders)
            => this.responseHeaders = responseHeaders;

        public override void ProcessResponseStream(Stream dataStream)
        {
            try
            {
                if (dataStream == null) return;

                using (Stream stream = new MemoryStream())
                {
                    dataStream.CopyTo(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    using (var reader = new HtmlReader(stream))
                    {
                        while (!reader.EOF)
                        {
                            var node = reader.NextNode();
                            if (node.Type == HtmlTokenType.Text)
                            {
                                evaluationText.Append(node.Value);
                                evaluationText.Append(' ');
                            }

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

                    // evaluate against the queries
                    foreach (var qry in Queries)
                    {
                        try
                        {
                            stream.Seek(0, SeekOrigin.Begin);
                            var r = qry.Value.Evaluate(this, evaluationText.ToString(), stream);
                            if (r.Action == EvaluationResultAction.Include)
                            {
                                result.Tags.AddRange(r.Tags);
                                postTo.Add(qry.Key);
                            }
                        }
                        catch (Exception ex)
                        {
                            Utility.LogException(ex);
                        }
                    }

                    // if no matches, move onto the next resource
                    if (!postTo.Any()) return;

                    foreach (var workspaceId in postTo.ToArray())
                    {
                        fetchoClient.PostWorkspaceResultsByWorkspaceIdAsync(workspaceId, new WorkspaceResult[] { result }).GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
            finally
            {
                if (ResourcesSeen++ % 100 == 0)
                {
                    using (var db = new Database())
                    {
                        UpdateStatistics(db);
                        SetupQueries(db);
                    }
                }
            }
        }

        public override void NewResource()
            => ClearAll();

        string ReadDesc(HtmlReader reader)
        {
            if (reader.GetAttribute("name").Contains("description"))
                return reader.GetAttribute("content").Replace("\n", " ").Replace("\t", " ").Replace("\r", " ").Trim().Truncate(1024);
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
                    return title.Replace("\n", " ").Replace("\t", " ").Replace("\r", " ").Trim();
            }

            return title.Replace("\n", " ").Replace("\t", " ").Replace("\r", " ").Trim().Truncate(128);
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
            var workspaces = db.GetWorkspaces().GetAwaiter().GetResult();
            foreach (var workspace in workspaces.Distinct())
            {
                var id = workspace.WorkspaceId.GetValueOrDefault();
                if (!workspace.IsActive || String.IsNullOrWhiteSpace(workspace.QueryText))
                {
                    if (Queries.ContainsKey(id))
                        Queries.Remove(id);
                }
                else if (!Queries.ContainsKey(id))
                {
                    Queries.Add(id, new Query(workspace.QueryText));
                }
                else if(Queries[id].OriginalQueryText != workspace.QueryText)
                {
                    Queries[id] = new Query(workspace.QueryText);
                }

            }
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
    }


}
