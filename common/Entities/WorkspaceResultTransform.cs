using System;
using System.Collections.Generic;

namespace Fetcho.Common.Entities
{
    public class WorkspaceResultTransform
    {
        public WorkspaceResultTransformAction Action { get; set; }

        public IEnumerable<WorkspaceResult> Results { get; set; }

        public string QueryText { get; set; }

        public WorkspaceResultTransform()
        {
            Action = WorkspaceResultTransformAction.None;
            Results = null;
            QueryText = String.Empty;
        }
    }

    public enum WorkspaceResultTransformAction
    {
        None,
        DeleteAll,
        DeleteSpecificResults,
        DeleteByQueryText,
        CopyAllTo,
        MoveAllTo,
        CopySpecificTo,
        MoveSpecificTo
    }
}
