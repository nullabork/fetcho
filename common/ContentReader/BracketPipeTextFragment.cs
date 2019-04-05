namespace Fetcho.ContentReaders
{
    public class BracketPipeTextFragment
    {
        public string Tag { get; set; }

        public string Text { get; set; }

        public BracketPipeTextFragment(string tag, string text)
        {
            Tag = tag;
            Text = text;
        }
    }
}
