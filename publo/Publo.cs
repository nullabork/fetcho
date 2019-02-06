using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using Fetcho.Common;
using log4net;

namespace Fetcho.Publo
{
    public class Publo
    {
        public const int MaxWebResources = 50;

        public PubloConfiguration Configuration { get; set; }
        public List<PubloWebResource> WebResources { get; set; }
        private Random random = new Random(DateTime.Now.Millisecond);
        private readonly static ILog log = LogManager.GetLogger(typeof(Publo));

        public Publo(PubloConfiguration config)
        {
            Configuration = config;
            WebResources = new List<PubloWebResource>();
        }

        public void Process()
        {
            // read in a packet and pull out the titles, urls and descriptions

            Random random = new Random(DateTime.Now.Millisecond);

            using (var stream = Configuration.InStream)
            {
                var packet = new WebDataPacketReader(stream);

                try
                {
                    while (packet.NextResource())
                    {
                        var r = ProcessPacket(packet);
                        if (AddToTheList(r))
                            WebResources.Add(r);

                        if (QuotaReached())
                            break;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }

            OutputWebResources();
        }

        bool QuotaReached() => WebResources.Count == 50;

        bool AddToTheList(PubloWebResource resource) => 
            resource != null && 
            !resource.IsEmpty && 
            !String.IsNullOrWhiteSpace(resource.Title) && 
            random.Next(0, 1000) <= 30;

        PubloWebResource ProcessPacket(WebDataPacketReader packet)
        {
            try
            {
                string request = packet.GetRequestString();

                if (string.IsNullOrWhiteSpace(request)) throw new Exception("No good");

                var uri = WebDataPacketReader.GetUriFromRequestString(request);
                if (uri == null)
                    return null;
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

        string ReadDesc(string line)
        {
            const string StartPoint = "content=\"";
            var sb = new StringBuilder();

            int start = line.ToLower().IndexOf(StartPoint);
            if (start == -1) return "";
            start += StartPoint.Length - 1;
            while (++start < line.Length && line[start] != '"') // </title>
                sb.Append(line[start]);

            return sb.ToString().Trim();
        }


        string ReadTitle(string line)
        {
            var sb = new StringBuilder();

            int start = line.ToLower().IndexOf("<title");
            while (++start < line.Length && line[start] != '>') ;
            while (++start < line.Length && line[start] != '<') // </title>
                sb.Append(line[start]);

            return sb.ToString().Trim();
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
                Created = DateTime.Now,
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
        public string Description { get; set; }
        public string Title { get; set; }
        public int Size { get; set; }

        [ScriptIgnore]
        public bool IsEmpty
        {
            get { return String.IsNullOrWhiteSpace(Uri); }
        }
    }
}
