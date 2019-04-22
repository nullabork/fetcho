namespace Fetcho.Common.Net
{
    public class HackerNewsItem
    {
        public string by { get; set; }
        public string descendants { get; set; }
        public int id { get; set; }
        public int[] kids { get; set; }
        public int score { get; set; }
        public int time { get; set; }
        public string title { get; set; }
        public string type { get; set; }
        public string url { get; set; }
    }


}
