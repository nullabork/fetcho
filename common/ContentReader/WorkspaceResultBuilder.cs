using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using BracketPipe;
using Fetcho.Common;
using Fetcho.Common.Entities;

namespace Fetcho.ContentReaders
{
    /// <summary>
    /// Parses a stream to build a WorkspaceResult
    /// </summary>
    /// TODO: I don't really like the way IWebResource works since the properties are on the builder
    public class WorkspaceResultBuilder
    {
        private StringBuilder evaluationText = new StringBuilder();


        public WorkspaceResult Build(Stream stream, string requestString, string responseHeaders, out string evalText)
        {
            WorkspaceResult result = new WorkspaceResult();

            if (!stream.CanSeek)
                throw new FetchoException("WorkspaceResultBuilder needs a seekable stream");

            ProcessHeaders(result, requestString, responseHeaders);
            result.DataHash = MD5Hash.Compute(stream).ToString();
            result.PageSize = stream.Length;
            stream.Seek(0, SeekOrigin.Begin);

            using (var reader = new HtmlReader(stream))
            {
                while (!reader.EOF)
                {
                    var node = reader.NextNode();
                    if (node.Type == HtmlTokenType.Text)
                    {
                        evaluationText.Append(node.Value);
                        evaluationText.Append(' ');
                    }

                    if (node.Value == "script")
                        ReadToEndTag(reader, "script");

                    if (node.Value == "style")
                        ReadToEndTag(reader, "style");

                    if (node.Value == "title" && !result.PropertyCache.ContainsKey("title"))
                    {
                        string title = ReadTitle(reader);
                        result.PropertyCache.Add("title", title);
                    }

                    if (node.Value == "meta")
                    {
                        var desc = ReadDesc(reader);
                        if (!String.IsNullOrWhiteSpace(desc))
                        {
                            if (!result.PropertyCache.ContainsKey("description"))
                                result.PropertyCache.Add("description", desc);
                            result.PropertyCache["description"] = desc;
                        }
                    }
                }
            }

            result.UriHash = MD5Hash.Compute(result.RequestProperties["uri"]).ToString();
            result.RefererUri = result.RequestProperties.SafeGet("referer");
            result.Uri = result.RequestProperties["uri"];
            result.Title = result.PropertyCache.SafeGet("title")?.ToString();
            result.Description = result.PropertyCache.SafeGet("desc")?.ToString();
            result.Created = DateTime.UtcNow;
            result.Updated = DateTime.UtcNow;
            result.DebugInfo = "Source: QueryConsumer\n";


            evalText = evaluationText.ToString();

            return result;
        }

        string ReadDesc(HtmlReader reader)
        {
            if (reader.GetAttribute("name").Contains("description"))
                return WebUtility.HtmlDecode(reader.GetAttribute("content").Replace("\n", " ").Replace("\t", " ").Replace("\r", " ").Trim()).Truncate(1024);
            return String.Empty;
        }

        string ReadTitle(HtmlReader reader)
        {
            string title = "";

            while (!reader.EOF)
            {
                var node = reader.NextNode();

                if (node.Type == HtmlTokenType.Text)
                    title += node.Value;

                if (node.Type == HtmlTokenType.EndTag && node.Value == "title")
                    return WebUtility.HtmlDecode(title.Replace("\n", " ").Replace("\t", " ").Replace("\r", " ").Trim()).Truncate(128);
            }

            return WebUtility.HtmlDecode(title.Replace("\n", " ").Replace("\t", " ").Replace("\r", " ").Trim()).Truncate(128);
        }

        void ReadToEndTag(HtmlReader reader, string endTag)
        {
            while (!reader.EOF)
            {
                var n = reader.NextNode();

                if (n.Value == endTag && n.Type == HtmlTokenType.EndTag)
                    return;
            }
        }

        void ProcessHeaders(WorkspaceResult result, string requestString, string responseHeaders)
        {
            if (!String.IsNullOrWhiteSpace(requestString))
            {
                var lines = requestString.Split('\n');
                foreach (var line in lines)
                {
                    int idx = line.IndexOf(':');
                    if (idx < 0) continue;
                    string key = line.Substring(0, idx).Trim().ToLower();
                    string value = line.Substring(idx + 1).Trim();

                    if (!String.IsNullOrWhiteSpace(key) && !result.RequestProperties.ContainsKey(key))
                        result.RequestProperties.Add(key, value);
                }
            }

            if (!String.IsNullOrWhiteSpace(responseHeaders))
            {
                var lines = responseHeaders.Split('\n');
                foreach (var line in lines)
                {
                    int idx = line.IndexOf(':');
                    if (idx < 0) continue;
                    string key = line.Substring(0, idx).Trim().ToLower();
                    string value = line.Substring(idx + 1).Trim();

                    if (!String.IsNullOrWhiteSpace(key) && !result.ResponseProperties.ContainsKey(key))
                        result.ResponseProperties.Add(key, value);
                }
            }
        }
    }
}
