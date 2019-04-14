using Fetcho.Common;
using System.IO;
using System.Threading.Tasks;

namespace Fetcho
{
    public class ReadoProcessor
    {
        public WebDataPacketProcessor Processor { get; }

        public ReadoProcessor()
        {
            Processor = new WebDataPacketProcessor();
        }

        public async Task Process(string filepath)
        {
            ThrowIfFileDoesntExist(filepath);
            using (var fs = GetFileStream(filepath))
            {
                Utility.LogInfo(filepath);
                var packet = new WebDataPacketReader(fs);
                await Processor.Process(packet);
            }
        }

        private Stream GetFileStream(string filepath)
            => Utility.GetDecompressedStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        private void ThrowIfFileDoesntExist(string packetPath)
        {
            if (!File.Exists(packetPath))
                throw new FileNotFoundException(packetPath);
        }

    }
}
