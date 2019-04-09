using System;
using Fetcho.Common.QueryEngine;

namespace Fetcho.Common.Entities
{
    public class WorkspaceQueryStats
    {
        public Guid WorkspaceId { get; set; }
        public long MaxCost { get; set; }
        public long AvgCost { get; set; }
        public long TotalCost { get; set; }
        public long NumberOfEvaluations { get; set; }
        public long NumberOfInclusions { get; set; }
        public long NumberOfExclusions { get; set; }
        public long NumberOfTags { get; set; }
        public long Sequence { get; set; }
        public DateTime Created { get; set; }

        public WorkspaceQueryStats()
        {
            WorkspaceId = Guid.Empty;
            MaxCost = -1;
            AvgCost = -1;
            TotalCost = -1;
            NumberOfEvaluations = 0;
            NumberOfInclusions = 0;
            NumberOfExclusions = 0;
            NumberOfTags = 0;
            Sequence = 0;
            Created = DateTime.Now;
        }

        public WorkspaceQueryStats(Guid workspaceId, Query qry)
        {
            WorkspaceId = workspaceId;
            MaxCost = qry.MaxCost;
            AvgCost = qry.AvgCost;
            TotalCost = qry.TotalCost;
            NumberOfEvaluations = qry.NumberOfEvaluations;
            NumberOfExclusions = qry.NumberOfExclusions;
            NumberOfInclusions = qry.NumberOfInclusions;
            NumberOfTags = qry.NumberOfTags;
            Sequence = 0;
            Created = DateTime.Now;
        }
    }
}