using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fetcho.Publo
{
    public class PubloConfiguration
    {
        public Stream InStream { get; set; }
        public TextWriter OutStream { get; set; }

        public PubloConfiguration(string[] args)
        {
            InStream = new FileStream(args[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            OutStream = Console.Out;
        }
    }
}
