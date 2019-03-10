using System;

namespace Fetcho.Common.Entities
{

    public class WorkspaceAccessKey
    {
        public Guid Id { get; set; }

        public string AccessKey { get; set; }

        public DateTime Expiry { get; set; }

        public bool IsActive { get; set; }

        public DateTime Created { get; set; }

        public WorkspaceAccessPermissions Permissions { get; set; }

        public WorkspaceAccessKey()
        {
            IsActive = true;
            Expiry = DateTime.MaxValue;
            Created = DateTime.Now;
            Permissions = WorkspaceAccessPermissions.None;
            Id = Guid.NewGuid();
        }

        public static WorkspaceAccessKey Create(WorkspaceAccessPermissions permissions) => new WorkspaceAccessKey()
        {
            Id = Guid.NewGuid(),
            AccessKey = Utility.GetRandomHashString(),
        };
    }
}
