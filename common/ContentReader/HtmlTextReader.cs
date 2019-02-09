using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Fetcho.ContentReaders
{
    public class HtmlTextReader : StreamReader
    {
        private int currentChar = -1;

        public HtmlTextReader(Stream stream) : base(stream) => currentChar = ReadToEndOfTag();
        
        public override int Peek() => currentChar;

        public override int Read()
        {
            int i = currentChar;
            currentChar = base.Read();
            if (IsStartTag(currentChar)) currentChar = ReadToEndOfTag();
            return i;
        }

        public override Task<int> ReadAsync(char[] buffer, int index, int count) => throw new NotImplementedException();

        public override int Read(char[] buffer, int index, int count) => throw new NotImplementedException();

        public override int ReadBlock(char[] buffer, int index, int count) => throw new NotImplementedException();

        public override Task<int> ReadBlockAsync(char[] buffer, int index, int count) => throw new NotImplementedException();

        public override string ReadLine()
        {
            var sb = new StringBuilder();

            int i = Peek();
            if (i < 0) return null;
            while (!IsStartTag(i) && i != (int)'\n' && i != (int)'\r')
            {
                sb.Append((char)Read());
                i = base.Peek();
            }

            return sb.ToString();
        }

        public override string ReadToEnd()
        {
            var sb = new StringBuilder();

            int i = Peek();
            while ( i >= 0 )
            {
                sb.Append((char)i);
                i = Read();
            }

            return sb.ToString();
        }

        public override Task<string> ReadToEndAsync() => throw new NotImplementedException();

        public override Task<string> ReadLineAsync() => throw new NotImplementedException();

        private int ReadToEndOfTag()
        {
            int i = base.Read();
            if (!IsStartTag(i)) return i;
            while (i >= 0 && !IsEndTag(i)) i = base.Read();
            return base.Read();
        }

        private bool IsStartTag(int c) => c == (int)'<';
        private bool IsEndTag(int c) => c == (int)'>';

    }
}
