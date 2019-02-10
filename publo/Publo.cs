using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using Fetcho.Common;
using log4net;

namespace Fetcho.Publo
{
    public class Publo
    {
        public const long AverageSizePerPage = 140000; //bytes
        public const int MaxWebResources = 50;

        public PubloConfiguration Configuration { get; set; }
        public List<PubloWebResource> WebResources { get; set; }
        private Random random = new Random(DateTime.Now.Millisecond);
        private readonly static ILog log = LogManager.GetLogger(typeof(Publo));
        private int MaxRandomValue = 0;

        public Publo(PubloConfiguration config)
        {
            Configuration = config;
            MaxRandomValue = (int)(config.FileSize / AverageSizePerPage);
            log.Info("MaxRandomValue = " + MaxRandomValue);
            WebResources = new List<PubloWebResource>();
        }

        public void Process()
        {
            // read in a packet and pull out the titles, urls and descriptions

            Random random = new Random(DateTime.Now.Millisecond);

            using (var stream = Configuration.InStream)
            {
                var packet = new WebDataPacketReader(stream);

                int jumpTo = random.Next(0, MaxRandomValue);

                try
                {
                    while (jumpTo --> 0) packet.NextResource();
                    while (WebResources.Count < MaxWebResources)
                    {
                        var r = ProcessPacket(packet);
                        if (AddToTheList(r))
                            WebResources.Add(r);

                        if (!packet.NextResource()) break;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }

            OutputWebResources();
        }


        bool AddToTheList(PubloWebResource resource) =>
            resource != null &&
            !resource.IsEmpty &&
            !String.IsNullOrWhiteSpace(resource.Title); 

        PubloWebResource ProcessPacket(WebDataPacketReader packet)
        {
            try
            {
                string request = packet.GetRequestString();

                var uri = WebDataPacketReader.GetUriFromRequestString(request);
                if (uri == null) return null;
                var response = packet.GetResponseStream();
                if (response != null)
                    return ReadNextWebResource(uri, new StreamReader(response));
                return null;
            }
            catch( Exception ex )
            {
                log.Error(ex);
                return null;
            }
        }

        PubloWebResource ReadNextWebResource(Uri uri, TextReader reader)
        {
            var r = new PubloWebResource
            {
                Hash = MD5Hash.Compute(uri).ToString(),
                Uri = uri.ToString(),
                Title = "",
                Description = "",
                Size = 0
            };

            string line = reader.ReadLine();
            if (line == null)
                return r;
            else
            {

                r.Size += line.Length;
                while (reader.Peek() > 0)
                {
                    if (String.IsNullOrWhiteSpace(r.Title) && line.ToLower().Contains("<title")) r.Title = ReadTitle(line);
                    else if (String.IsNullOrWhiteSpace(r.Description) && line.ToLower().Contains("description")) r.Description = ReadDesc(line).Truncate(100);
                    line = reader.ReadLine();
                    r.Size += line.Length;
                }

                return r;
            }
        }

        string ReadDesc(string line)
        {
            const string StartPoint = "content=\"";
            var sb = new StringBuilder();

            int start = line.ToLower().IndexOf(StartPoint);
            if (start == -1) return "";
            start += StartPoint.Length - 1;
            while (++start < line.Length && line[start] != '"') // </meta >
                sb.Append(line[start]);

            return HttpUtility.HtmlDecode(sb.ToString().Trim());
        }


        string ReadTitle(string line)
        {
            var sb = new StringBuilder();

            int start = line.ToLower().IndexOf("<title");
            while (++start < line.Length && line[start] != '>') ;
            while (++start < line.Length && line[start] != '<') // </title>
                sb.Append(line[start]);

            return HttpUtility.HtmlDecode(sb.ToString().Trim());
        }

        string ReadUri(string line)
        {
            if (line.Length < 5) return "";
            return line.Substring(5).Trim();
        }

        void OutputWebResources()
        {
            var file = new PubloFile()
            {
                Created = DateTime.UtcNow,
                WebResources = WebResources.ToArray()
            };

            var serializer = new JavaScriptSerializer();
            using (var writer = Configuration.OutStream)
                writer.WriteLine(serializer.Serialize(file));
        }

    }

    public class PubloFile
    {
        public DateTime Created { get; set; }
        public PubloWebResource[] WebResources { get; set; }
    }

    public class PubloWebResource
    {
        public string Hash { get; set; }
        public string Uri { get; set; }
        public string ReferrerUri { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }
        public int Size { get; set; }
        public string[] Tags { get; set; }

        public PubloWebResource() { Tags = new string[] { }; }

        [ScriptIgnore]
        public bool IsEmpty
        {
            get { return String.IsNullOrWhiteSpace(Uri); }
        }
    }
}
