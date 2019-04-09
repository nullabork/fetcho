using System;
using System.Collections.Generic;
using System.Linq;

namespace Fetcho.Common.Entities
{
    public class WorkspaceResultTransform
    {
        /// <summary>
        /// The action you want to run
        /// </summary>
        public WorkspaceResultTransformAction Action { get; set; }

        /// <summary>
        /// For IsSpecificResultTransform the list of results to operate on
        /// </summary>
        public List<WorkspaceResult> Results { get; set; }

        /// <summary>
        /// For IsByQueryTransform the query text for parsing
        /// </summary>
        public string QueryText { get; set; }

        /// <summary>
        /// For Move and Copy to actions, the target workspace
        /// </summary>
        public Guid TargetAccessKeyId { get; set; }

        /// <summary>
        /// For Tag actions, the tag
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// After the processing this is the error message
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Will be set to true if success
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Returns the number of results affected
        /// </summary>
        public long ResultsAffected { get; set; }

        public bool IsByQueryTransform
        {
            get => Action == WorkspaceResultTransformAction.MoveByQueryText ||
                Action == WorkspaceResultTransformAction.TagByQueryText ||
                Action == WorkspaceResultTransformAction.DeleteByQueryText ||
                Action == WorkspaceResultTransformAction.UntagByQueryText ||
                Action == WorkspaceResultTransformAction.CopyByQueryText;
        }

        public bool IsSpecificResultTransform
        {
            get => Action == WorkspaceResultTransformAction.TagSpecificResults ||
                Action == WorkspaceResultTransformAction.DeleteSpecificResults ||
                Action == WorkspaceResultTransformAction.UntagSpecificResults ||
                Action == WorkspaceResultTransformAction.CopySpecificTo ||
                Action == WorkspaceResultTransformAction.CopyByQueryText;
        }

        public bool IsAllTransform
        {
            get => Action == WorkspaceResultTransformAction.DeleteAll ||
                Action == WorkspaceResultTransformAction.UntagAll ||
                Action == WorkspaceResultTransformAction.CopyAllTo ||
                Action == WorkspaceResultTransformAction.MoveAllTo ||
                Action == WorkspaceResultTransformAction.TagAll;
        }

        public bool IsTag
        {
            get => Action == WorkspaceResultTransformAction.TagAll ||
                Action == WorkspaceResultTransformAction.TagByQueryText ||
                Action == WorkspaceResultTransformAction.TagSpecificResults;
        }

        public bool IsDeleteTag
        {
            get => Action == WorkspaceResultTransformAction.UntagAll ||
                Action == WorkspaceResultTransformAction.UntagByQueryText ||
                Action == WorkspaceResultTransformAction.UntagSpecificResults;
        }

        public bool IsDelete
        {
            get => Action == WorkspaceResultTransformAction.DeleteAll ||
                Action == WorkspaceResultTransformAction.DeleteSpecificResults ||
                Action == WorkspaceResultTransformAction.DeleteByQueryText;
        }

        public bool IsCopy
        {
            get => Action == WorkspaceResultTransformAction.CopyAllTo ||
                Action == WorkspaceResultTransformAction.CopyByQueryText ||
                Action == WorkspaceResultTransformAction.CopySpecificTo;
        }

        public bool IsMove
        {
            get => Action == WorkspaceResultTransformAction.MoveAllTo ||
                Action == WorkspaceResultTransformAction.MoveByQueryText ||
                Action == WorkspaceResultTransformAction.MoveSpecificTo;
        }

        public bool HasTarget
        {
            get => IsMove || IsCopy;
        }

        public WorkspaceResultTransform()
        {
            Action = WorkspaceResultTransformAction.None;
            Results = null;
            QueryText = String.Empty;
            TargetAccessKeyId = Guid.Empty;
            Tag = "";
            Success = false;
            ErrorMessage = "";
            ResultsAffected = -1;
        }

        public static void Validate(WorkspaceResultTransform transform)
        {
            if (transform.IsSpecificResultTransform)
            {
                if (!transform.Results.Any(x => !String.IsNullOrWhiteSpace(x.UriHash)))
                    throw new InvalidObjectFetchoException("Specific result transforms require the Results property to be filled in with UriHash");
            }

            if (transform.IsCopy || transform.IsMove)
            {
                if (transform.TargetAccessKeyId == Guid.Empty)
                    throw new InvalidObjectFetchoException("Copy, move and tag require a TargetAccessKeyId");
            }

            if (transform.IsByQueryTransform)
            {
                if (String.IsNullOrWhiteSpace(transform.QueryText))
                    throw new InvalidObjectFetchoException("QueryText is required to be passed when using a transform by query text");
            }

            if (transform.IsDeleteTag || transform.IsTag)
            {
                if (String.IsNullOrWhiteSpace(transform.Tag))
                    throw new InvalidObjectFetchoException("Tag is required to be passed when using a tagging transform");
            }

        }
    }

    public enum WorkspaceResultTransformAction
    {
        None,
        DeleteAll,
        DeleteSpecificResults,
        DeleteByQueryText,

        CopyAllTo,
        CopySpecificTo,
        CopyByQueryText,

        MoveAllTo,
        MoveSpecificTo,
        MoveByQueryText,

        TagAll,
        TagSpecificResults,
        TagByQueryText,

        UntagAll,
        UntagSpecificResults,
        UntagByQueryText
    }
}
