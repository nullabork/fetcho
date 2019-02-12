using System;

namespace Fetcho.Common
{
    public class WorkspaceAccessKey
    {
        public string AccessKey { get; set; }

        public bool IsOwner { get; set; }

        public DateTime Expiry { get; set; }

        public bool IsActive { get; set; }

        public DateTime Created { get; set; }

        public WorkspaceAccessKey()
        {
            IsActive = true;
            Expiry = DateTime.MaxValue;
            Created = DateTime.Now;
        }

        public static WorkspaceAccessKey Create(bool isOwner = true) => new WorkspaceAccessKey()
        {
            AccessKey = Utility.GetRandomHashString(),
            IsOwner = isOwner
        };
    }
}
