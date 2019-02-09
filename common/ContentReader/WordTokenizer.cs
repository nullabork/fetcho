using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting;
using System.Text;

namespace Fetcho.ContentReaders
{


    public class WordTokenizer : IDisposable, ITokenizer, IEnumerable<string>
    {
        public Func<char, bool> GapFunc { get; set; }
        public Func<char, bool> RemoveFunc { get; set; }

        public string QuoteOpenChars { get; set; }
        public string QuoteClosureChars { get; set; }

        public TextReader Stream { get; set; }
        public bool LeaveOpen { get; set; }
        public bool EndOfStream { get; set; }

        private Queue<string> tokenQueue = new Queue<string>();

        public WordTokenizer()
        {
            RemoveFunc = DefaultRemoveCharFunc;
            GapFunc = DefaultGapCharFunc;
            QuoteOpenChars = "";
            QuoteClosureChars = "";
            EndOfStream = false;
            LeaveOpen = false;
        }

        public WordTokenizer(string words) : this(new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(words))), false)
        {
        }

        public WordTokenizer(Stream stream, bool leaveOpen = false) : this(new StreamReader(stream), leaveOpen)
        {
        }

        public WordTokenizer(TextReader reader, bool leaveOpen = false) : this()
        {
            Stream = reader;
            LeaveOpen = leaveOpen;
        }

        public string[] GetAllTokens()
        {
            var l = new List<string>();

            while (!EndOfStream)
                l.Add(NextToken());

            return l.ToArray();
        }

        public void Reset()
        {
        }

        public string NextToken()
        {
            if (tokenQueue.Count > 0)
                return tokenQueue.Dequeue();

            var sb = new StringBuilder();

            char c = Read();
            if (GapFunc.Invoke(c) && !RemoveFunc.Invoke(c))
            {
                sb.Append(c);
                return sb.ToString();
            }

            while (GapFunc.Invoke(c) && !EndOfStream) // skip whitespace
            {
                c = Read();
                if (!RemoveFunc.Invoke(c))
                {
                    sb.Append(c);
                    return sb.ToString();
                }
            }

            if (QuoteOpenChars.IndexOf(c) >= 0)
            {
                c = Read();

                while (QuoteClosureChars.IndexOf(c) < 0 && !EndOfStream)
                {
                    if (!RemoveFunc.Invoke(c))
                        sb.Append(c);
                    c = Read();
                }
            }
            else
            {
                while (!GapFunc.Invoke(c) && !EndOfStream)
                {
                    if (!RemoveFunc.Invoke(c))
                        sb.Append(c);
                    c = Read();
                }
            }

            if (!RemoveFunc.Invoke(c))
            {
                tokenQueue.Enqueue(c.ToString());
            }

            return sb.ToString();
        }

        private char Peek()
        {
            int i = Stream.Peek();
            if (i > 0)
                return Convert.ToChar(i);
            else
            {
                EndOfStream = true;
                return '\0';
            }
        }

        private char Read()
        {
            int i = Stream.Read();
            if (i > 0)
                return Convert.ToChar(i);
            else
            {
                EndOfStream = true;
                return '\0';
            }
        }

        public void Close()
        {
            if (!LeaveOpen)
                Stream.Close();
        }


        public void Dispose()
        {
            if (!LeaveOpen)
                Stream.Dispose();
        }

        public IEnumerator<string> GetEnumerator() => new WordTokenizerEnumerator(this);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => new WordTokenizerEnumerator(this);

        public static bool DefaultRemoveCharFunc(char c) =>
            char.IsPunctuation(c) ||
            char.IsControl(c) ||
            char.IsWhiteSpace(c);

        public static bool DefaultGapCharFunc(char c) =>
              char.IsSymbol(c) ||
              char.IsDigit(c) ||
              char.IsWhiteSpace(c) ||
              char.IsSeparator(c) ||
              char.IsControl(c) ||
              char.IsPunctuation(c);

        private class WordTokenizerEnumerator : IEnumerator<string>
        {
            public WordTokenizer Tokenizer { get; set; }

            public string Current { get { return _current; } }

            object System.Collections.IEnumerator.Current { get { return _current; } }
            private string _current = String.Empty;

            public WordTokenizerEnumerator(WordTokenizer tokenizer)
            {
                Tokenizer = tokenizer;
            }

            public void Dispose()
            {
                Tokenizer = null;
            }

            public bool MoveNext()
            {
                if (Tokenizer.EndOfStream)
                    return false;
                else
                {
                    _current = Tokenizer.NextToken();
                    return true;
                }
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
    }
}
