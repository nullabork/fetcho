using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Fetcho.Common.Net
{
    public static class HackerNewsFrontPageFetcher
    {
        const string YCombinatorUri = "https://news.ycombinator.com/front?day={0:yyyy-MM-dd}";
        const string FirebaseUri = "https://hacker-news.firebaseio.com/v0/item/{0}.json?print=pretty";

        public static async Task<IEnumerable<Uri>> GetLinks(DateTime start, DateTime end)
        {
            IEnumerable<Uri> l = new Uri[0];
            do
            {
                l = l.Concat(await GetLinks(start));
            }
            while (start.AddDays(1) <= end);
            return l;
        }

        public static async Task<IEnumerable<Uri>> GetLinks(DateTime day)
        {
            var l = new List<Uri>();
            using (var client = new HttpClient())
            {
                var str = await client.GetStringAsync(string.Format(YCombinatorUri, day));
                var doc = new HtmlAgilityPack.HtmlDocument();

                doc.LoadHtml(str);

                foreach (var node in doc.DocumentNode.SelectNodes("//a[contains(@href,'item?id=')]"))
                {
                    var href = node.GetAttributeValue("href", "");
                    var id = GetHrefId(href);

                    if ( int.TryParse(id, out int idint))
                    {
                        var item = await GetItem(idint);
                        if ( item != null && item?.type == "story" && item?.url?.Length > 0 && Uri.IsWellFormedUriString(item?.url, UriKind.Absolute))
                            l.Add(new Uri(item.url));
                    }
                }
            }
            return l;
        }

        private static async Task<HackerNewsItem> GetItem(int id)
        {
            using (var client = new HttpClient())
            {
                var uri = string.Format(FirebaseUri, id);
                var response = await client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsAsync<HackerNewsItem>();
            }
        }

        private static string GetHrefId(string href)
        {
            int idx = href.IndexOf("=");
            if (idx > -1)
                return href.Substring(idx + 1);
            return "";
        }
    }


}
