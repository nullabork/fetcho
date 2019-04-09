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

        public int Revision { get; set; }

        [JsonIgnore]
        public bool IsOwner { get => HasPermissionFlags(WorkspaceAccessPermissions.Owner); }

        [JsonIgnore]
        public bool CanRead
        {
            get =>  HasPermissionFlags(WorkspaceAccessPermissions.Owner)
                    || HasPermissionFlags(WorkspaceAccessPermissions.Read);
        }

        [JsonIgnore]
        public bool CanWrite
        {
            get =>  HasPermissionFlags(WorkspaceAccessPermissions.Owner)
                    || HasPermissionFlags(WorkspaceAccessPermissions.Write);
        }

        [JsonIgnore]
        public bool CanManage
        {
            get => HasPermissionFlags(WorkspaceAccessPermissions.Owner)
                   || HasPermissionFlags(WorkspaceAccessPermissions.Manage);
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
            Revision = 0;
        }

        public bool HasPermissionFlags(WorkspaceAccessPermissions flags)
            => (Permissions & flags) != 0;

        public static void Validate(AccessKey accessKey)
        {
            if (accessKey.Id == Guid.Empty)
                throw new InvalidObjectFetchoException("No ID set");
            if (accessKey.Permissions < 0)
                throw new InvalidObjectFetchoException("Invalid permissions");
            if (accessKey.Permissions >= WorkspaceAccessPermissions.Max)
                throw new InvalidObjectFetchoException("Permissions invalid");
            if (String.IsNullOrWhiteSpace(accessKey.AccountName))
                throw new InvalidObjectFetchoException("Account Name not set");
            if (String.IsNullOrWhiteSpace(accessKey.Name))
                throw new InvalidObjectFetchoException("Name not set");
        }
    }
}
