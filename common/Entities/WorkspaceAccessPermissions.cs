using System;

namespace Fetcho.Common.Entities
{
    [Flags]
    public enum WorkspaceAccessPermissions
    {
        None = 0,
        Owner = 1,
        Manage = 2,
        Read = 4,
        Write = 8,
        Max = 16
    }
}
