using System;

namespace Fetcho.Common.QueryEngine
{
    [Flags]
    public enum FilterMode
    {
        None = 0,
        Include = 1,
        Exclude = 2,
        Tag = 4
    }
}
