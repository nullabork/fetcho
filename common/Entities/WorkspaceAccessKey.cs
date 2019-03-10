using System;
using Newtonsoft.Json;

namespace Fetcho.Common.Entities
{
    public class WorkspaceAccessKey
    {
        public Guid Id { get; set; }

        public string AccessKey { get; set; }

        public DateTime Expiry { get; set; }

        public DateTime Created { get; set; }

        public bool IsWellKnown { get; set; }

        public WorkspaceAccessPermissions Permissions { get; set; }

        public bool IsActive { get; set; }

        [JsonIgnore]
        public bool IsOwner { get => HasPermissionFlag(WorkspaceAccessPermissions.Owner); }

        [JsonIgnore]
        public bool CanRead
        {
            get =>  HasPermissionFlag(WorkspaceAccessPermissions.Owner)
                    || HasPermissionFlag(WorkspaceAccessPermissions.Read);
        }

        [JsonIgnore]
        public bool CanWrite
        {
            get =>  HasPermissionFlag(WorkspaceAccessPermissions.Owner)
                    || HasPermissionFlag(WorkspaceAccessPermissions.Write);
        }

        [JsonIgnore]
        public bool CanManage
        {
            get => HasPermissionFlag(WorkspaceAccessPermissions.Owner)
                   || HasPermissionFlag(WorkspaceAccessPermissions.Manage);
        }

        public WorkspaceAccessKey()
        {
            IsActive = true;
            Expiry = DateTime.MaxValue;
            Created = DateTime.Now;
            Permissions = WorkspaceAccessPermissions.None;
            IsWellKnown = false;
            Id = Guid.NewGuid();
        }

        public bool HasPermissionFlag(WorkspaceAccessPermissions flag)
            => (Permissions & flag) == flag;

        public static WorkspaceAccessKey Create(WorkspaceAccessPermissions permissions)
            => new WorkspaceAccessKey()
            {
                Id = Guid.NewGuid(),
                AccessKey = Utility.GetRandomHashString(),
            };
    }
}
