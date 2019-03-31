using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
            Name = category;
            EndPoint = endPoint;
        }

        public override IEnumerable<TextPageData> GetData(int maxRecords)
        {
            var l = new List<TextPageData>(maxRecords);

            Uri previous = null;
            Uri current = null;

            foreach (var p in l)
            {
                try
                {
                    var submissions = RedditSubmissionFetcher.GetSubmissions(EndPoint).GetAwaiter().GetResult();

                    var client = new HttpClient();

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
