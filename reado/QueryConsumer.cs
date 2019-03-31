using Fetcho.Common;
using Fetcho.Common.Entities;
using Fetcho.Common.QueryEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fetcho.Common.Net;
using Fetcho.ContentReaders;

namespace Fetcho
{
    /// <summary>
    /// Used to find links that match specific queries
    /// </summary>
    internal class QueryConsumer : WebDataPacketConsumer
    {
        Dictionary<Guid, Query> Queries { get; }

        private FetchoAPIV1Client fetchoClient;
        private List<Guid> postTo = new List<Guid>();
        private string requestString = String.Empty;
        private string responseHeaders = String.Empty;


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
                    var builder = new WorkspaceResultBuilder();
                    var result = builder.Build(stream, requestString, responseHeaders, out string evaluationText);

                    // evaluate against the queries
                    foreach (var qry in Queries.OrderBy( x => x.Value.AvgCost ))
                    {
                        try
                        {
                            stream.Seek(0, SeekOrigin.Begin);
                            var r = qry.Value.Evaluate(result, evaluationText.ToString(), stream);
                            if (r.Action == EvaluationResultAction.Include)
                            {

                                result.Tags.AddRange(r.Tags.Distinct().Where( x => !result.Tags.Contains(x)));
                                result.DebugInfo += String.Format("Cost: {0}\nQuery stats:{1}\n", r.Cost, qry.Value.CostDetails());
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

                    stream.Seek(0, SeekOrigin.Begin);
                    fetchoClient.PostWebResourceDataCache(stream).GetAwaiter().GetResult();
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
                if (++ResourcesSeen % 1000 == 0)
                {
                    using (var db = new Database())
                    {
                        UpdateStatistics(db);
                        SetupQueries(db);
                        ReportStatus();
                    }
                }
            }
        }

        public override void NewResource()
            => ClearAll();

        DateTime lastCall = DateTime.UtcNow;
        long lastResourcesSeen = 0;

        void ReportStatus()
        {
            var timing = DateTime.UtcNow - lastCall;
            var diff = ResourcesSeen - lastResourcesSeen;
            lastResourcesSeen = ResourcesSeen;
            lastCall = DateTime.UtcNow;
            Console.Clear();
            Console.WriteLine("\nProcessed: {0}, {1:0.00}/min", ResourcesSeen, (double)diff / timing.TotalMinutes);
            Console.WriteLine("MinCost    MaxCost    AvgCost    TotalCost   # Eval     # Inc      # Exc      # Tag      Query");
            foreach ( var qry in Queries.OrderByDescending(x => x.Value.AvgCost))
            {
                var query = qry.Value;
                Console.WriteLine("{0,-10} {1, -10} {2,-10} {3,-11} {4,-10} {5,-10} {6,-10} {7,-10} {8}",
                    query.MinCost, query.MaxCost, query.AvgCost, query.TotalCost, query.NumberOfEvaluations,
                    query.NumberOfInclusions, query.NumberOfExclusions, query.NumberOfTags, query.ToString().Truncate(128));
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
            requestString = String.Empty;
            responseHeaders = String.Empty;
        }


    }


}
