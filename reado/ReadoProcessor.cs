using Fetcho.Common;
using System.IO;
using System.Threading.Tasks;

namespace Fetcho
{
    public class ReadoProcessor
    {
        public WebDataPacketProcessor Processor { get;  }

        public ReadoProcessor()
        {
            Processor = new WebDataPacketProcessor();
        }

        public async Task Process(string filepath)
        {
            await Task.Run(() =>
            {
                ThrowIfFileDoesntExist(filepath);
                using (var fs = GetFileStream(filepath))
                {
                    var packet = new WebDataPacketReader(fs);
                    Processor.Process(packet);
                }
            });
        }

        private FileStream GetFileStream(string filepath) => new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        private void ThrowIfFileDoesntExist(string packetPath)
        {
            if (!File.Exists(packetPath))
                throw new FileNotFoundException(packetPath);
        }

    }
}
