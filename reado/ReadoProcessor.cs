using Fetcho.Common;
using System.IO;

namespace Fetcho
{
    public class ReadoProcessor
    {
        public WebDataPacketProcessor Processor { get;  }

        public ReadoProcessor()
        {
            Processor = new WebDataPacketProcessor();
        }

        public void Process(string filepath)
        {
            CheckPacketPath(filepath);
            using (var fs = GetFileStream(filepath))
            {
                var packet = new WebDataPacketReader(fs);
                Processor.Process(packet);
            }
        }

        private FileStream GetFileStream(string filepath) => new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);

        private void CheckPacketPath(string packetPath)
        {
            if (!File.Exists(packetPath))
                throw new FileNotFoundException(packetPath);
        }

    }
}
