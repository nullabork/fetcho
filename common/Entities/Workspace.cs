using System;
using System.Collections.Generic;
using System.Linq;

namespace Fetcho.Common.Entities
{
    /// <summary>
    /// A conceptual object that contains all the queries, filters, results and information for a specific search
    /// </summary>
    public class Workspace
    {
        /// <summary>
        /// GUID for the workspace
        /// </summary>
        public Guid WorkspaceId { get; set; }

        /// <summary>
        /// Name of the workspace
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of the workspace
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Query search text
        /// </summary>
        public string QueryText { get; set; }

        /// <summary>
        /// Date this workspace was created
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Is the workspace active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Is the workspace a well known workspace
        /// </summary>
        /// <remarks>Used for public work spaces</remarks>
        public bool IsWellKnown { get; set; }

        /// <summary>
        /// Number of results in the workspace
        /// </summary>
        public long ResultCount { get; set; }

        /// <summary>
        /// list of revokable keys that can access this workspace
        /// </summary>
        public List<WorkspaceAccessKey> AccessKeys { get;  }

        public Workspace()
        {
            AccessKeys = new List<WorkspaceAccessKey>();
        }

        /// <summary>
        /// Get the owner's access key
        /// </summary>
        /// <returns></returns>
        public WorkspaceAccessKey GetOwnerAccessKey() => AccessKeys.FirstOrDefault(x => (x.Permissions & WorkspaceAccessPermissions.Owner) == WorkspaceAccessPermissions.Owner);

        /// <summary>
        /// Determine if an access key has access to this workspace
        /// </summary>
        /// <param name="accessKey"></param>
        /// <returns></returns>
        public bool HasAccess(string accessKey) => AccessKeys.Any(x => x.AccessKey == accessKey);

        /// <summary>
        /// Create a workspace object
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Workspace Create(string name)
        {
            var w = new Workspace()
            {
                WorkspaceId = Guid.NewGuid(),
                Name = name,
                Description = "",
                QueryText = "",
                IsActive = true,
                Created = DateTime.UtcNow,
                ResultCount = 0
            };
            w.AccessKeys.Add(WorkspaceAccessKey.Create(WorkspaceAccessPermissions.Owner));

            return w;
        }

        /// <summary>
        /// Validate a workspace object
        /// </summary>
        /// <param name="workspace"></param>
        public static void Validate(Workspace workspace)
        {
            if (String.IsNullOrWhiteSpace(workspace?.Name))
                throw new ArgumentException("Name not set");
            if (workspace.WorkspaceId == Guid.Empty)
                throw new ArgumentException("WorkspaceId is not set");
            if (workspace.Created == DateTime.MinValue)
                throw new ArgumentException("Created is not set");
        }
    }
}