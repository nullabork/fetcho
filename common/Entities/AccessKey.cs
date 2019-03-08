using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
