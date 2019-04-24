using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Fetcho.Common.Net
{
    public class RedditSubmissionFetcher
    {
        public const string ApiEndPoint = "https://api.pushshift.io/reddit/search/submission/";

        public static async Task<IEnumerable<RedditSubmission>> GetSubmissions(string subreddit)
        {
            var l = new List<RedditSubmission>();
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(ContentType.ApplicationJson));

            for (int i = 2012; i < 2020; i++)
            {
                for (int j = 2; j <= 12; j++)
                {
                    string path = string.Format("{0}?subreddit={1}&size=1000&after={2}-{3:00}-01&before={4}-{5:00}-01",
                        ApiEndPoint, subreddit, i, j - 1, i, j);

                    var response = await client.GetAsync(path);
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsAsync<dynamic>();

                    foreach (var child in json["data"])
                    {
                        if (child["url"] == null || child["link_flair_text"] == null) continue;

                        l.Add(new RedditSubmission
                        {
                            Url = child["url"],
                            LinkFlairText = child["link_flair_text"]
                        });
                    }
                }
            }

            return l;
        }
    }
}
