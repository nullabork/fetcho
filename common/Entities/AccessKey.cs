using System;
using Newtonsoft.Json;

namespace Fetcho.Common.Entities
{
    public class AccessKey
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string AccountName { get; set; }

        public DateTime Expiry { get; set; }

        public DateTime Created { get; set; }

        public bool IsWellknown { get; set; }

        public WorkspaceAccessPermissions Permissions { get; set; }

        public bool IsActive { get; set; }

        public Workspace Workspace { get; set; }

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

        [JsonIgnore]
        public bool IsRevoked { get => Permissions == WorkspaceAccessPermissions.None; }

        public AccessKey()
        {
            IsActive = true;
            Name = Utility.GetRandomHashString();
            Expiry = DateTime.MaxValue;
            Created = DateTime.UtcNow;
            Permissions = WorkspaceAccessPermissions.Read;
            IsWellknown = false;
            Id = Guid.NewGuid();
            AccountName = String.Empty;
        }

        public bool HasPermissionFlag(WorkspaceAccessPermissions flag)
            => (Permissions & flag) == flag;

        public static void Validate(AccessKey workspaceAccessKey)
        {
            if (workspaceAccessKey.Id == Guid.Empty)
                throw new InvalidObjectFetchoException("No ID set");
            if (workspaceAccessKey.Permissions < 0)
                throw new InvalidObjectFetchoException("Invalid permissions");
            if (workspaceAccessKey.Permissions >= WorkspaceAccessPermissions.Max)
                throw new InvalidObjectFetchoException("Permissions invalid");
            if (String.IsNullOrWhiteSpace(workspaceAccessKey.AccountName))
                throw new InvalidObjectFetchoException("Account Name not set");
            if (String.IsNullOrWhiteSpace(workspaceAccessKey.Name))
                throw new InvalidObjectFetchoException("Name not set");

        }
    }
}
