using System;
using System.IO;
using System.Threading.Tasks.Dataflow;

namespace Fetcho.Common.DataFlow
{
    public class BufferBlockObjectFileWriter<T> : BufferBlockObjectConsumer<T>
    {

        private string DirectoryPath;
        private string FileNamePrefix;

        public BufferBlockObjectFileWriter(string directoryPath, string fileNamePrefix, BufferBlock<T> inBuffer) 
            : base(CreateOutputWriter(directoryPath, fileNamePrefix), inBuffer)
        {
            DirectoryPath = directoryPath;
            FileNamePrefix = fileNamePrefix;
        }

        private static TextWriter CreateOutputWriter(string path, string prefix)
        {
            if (String.IsNullOrWhiteSpace(path)) return null;
            if (String.IsNullOrWhiteSpace(prefix)) return null;
            string filename = Path.Combine(path, prefix + ".txt");
            filename = Utility.CreateNewFileOrIndexNameIfExists(filename);
            return new StreamWriter(new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read));
        }
    }
}
