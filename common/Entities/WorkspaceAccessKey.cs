using System;

namespace Fetcho.Common.Entities
{

    public class WorkspaceAccessKey
    {
        public Guid Id { get; set; }

        public string AccessKey { get; set; }

        public bool IsOwner { get; set; }

        public DateTime Expiry { get; set; }

        public bool IsActive { get; set; }

        public bool IsRevoked { get; set; }

        public DateTime Created { get; set; }

        public WorkspaceAccessKey()
        {
            IsActive = true;
            IsRevoked = false;
            Expiry = DateTime.MaxValue;
            Created = DateTime.Now;
            Id = Guid.NewGuid();
        }

        public static WorkspaceAccessKey Create(bool isOwner = true) => new WorkspaceAccessKey()
        {
            Id = Guid.NewGuid(),
            AccessKey = Utility.GetRandomHashString(),
            IsOwner = isOwner
        };
    }
}
