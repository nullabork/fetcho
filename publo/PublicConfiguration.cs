using System;
using System.IO;

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
