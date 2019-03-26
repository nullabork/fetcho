using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Fetcho.Common;
using Fetcho.ContentReaders;

namespace learno
{
    public class RedditMLModelSchemaCategoryDataSource : MLModelSchemaCategoryDataSource
    {
        public string EndPoint { get; }

        public RedditMLModelSchemaCategoryDataSource(string category, string endPoint)
        {
            Category = category;
            EndPoint = endPoint;
        }

        public override IEnumerable<TextPageData> GetData(int maxRecords)
        {
            var l = new List<TextPageData>(maxRecords);

            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(ContentType.ApplicationJson));

            for (int i = 2012; i < 2020; i++)
            {
                for (int j = 2; j <= 12; j++)
                {
                    string path = String.Format("https://api.pushshift.io/reddit/search/submission/?subreddit={0}&size=1000&after={1}-{2:00}-01&before={3}-{4:00}-01", EndPoint, i, j - 1, i, j);
                    var response = client.GetAsync(path).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    var json = response.Content.ReadAsAsync<dynamic>().GetAwaiter().GetResult();

                    foreach (var child in json["data"])
                    {
                        if (child["url"] == null || child["link_flair_text"] == null) continue;
                        Console.WriteLine("{0}: {1}",
                                            (string)child["link_flair_text"]?.ToString(),
                                            (string)child["url"]?.ToString()); // go fetch the page data
                        l.Add(new TextPageData
                        {
                            TextData = child["url"],
                            Category = child["link_flair_text"]
                        });
                    }
                }
            }

            Uri previous = null;
            Uri current = null;

            foreach (var p in l)
            {
                try
                {
                    previous = current;
                    current = new Uri(p.TextData);
                    using ( var stm = client.GetStreamAsync(current).GetAwaiter().GetResult())
                        p.TextData = BracketPipeTextExtractor.ReadAllText(stm).Aggregate("", (x, y) => x + " " + y);
                    if (previous != null && current.Host == previous.Host) Thread.Sleep(10000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            var newl = new List<TextPageData>(l.Count);
            foreach (var category in l.GroupBy(page => page.Category)
                                      .Select(category => new {
                                        Category = category.Key,
                                        Count = category.Count()
                                      }))
            {
                newl.AddRange( l.Where(x => x.Category == category.Category).Take(maxRecords));
            }

            return newl;
        }
    }
}
