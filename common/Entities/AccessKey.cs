using System;

namespace Fetcho.Common.Entities
{
    public class AccessKey
    {
        public string Key { get; set; }

        public DateTime Created { get; set; }

        public bool IsActive { get; set; }

        public AccessKey()
        {
            Created = DateTime.Now;
            IsActive = true;
            Key = Utility.GetRandomHashString();
        }
    }
}
