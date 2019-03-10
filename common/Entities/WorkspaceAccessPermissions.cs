using System;

namespace Fetcho.Common.Entities
{
    [Flags]
    public enum WorkspaceAccessPermissions
    {
        None = 0,
        Owner = 1
    }
}
