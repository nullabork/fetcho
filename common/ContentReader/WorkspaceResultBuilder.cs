using System;
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

            ContentType contentType = GetContentType(result);

            if (contentType != null)
            {
                if (contentType.SubType.Contains("html"))
                {
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
                                ProcessMetaTag(reader, result);
                            }
                        }
                    }
                }
                else if (contentType.IsTextType || ContentType.IsJavascriptContentType(contentType))
                {
                    // leave the stream open so other tasks can reset it and use it
                    using (var reader = new StreamReader(stream, Encoding.Default, true, 1024, true))
                    {
                        evaluationText.Append(reader.ReadToEnd());
                    }
                }
            }

            result.UriHash = MD5Hash.Compute(result.RequestProperties["uri"]).ToString();
            result.RefererUri = result.RequestProperties.SafeGet("referer");
            result.Uri = result.RequestProperties["uri"];
            result.Title = result.PropertyCache.SafeGet("title")?.ToString();
            result.Description = result.PropertyCache.SafeGet("description")?.ToString();
            result.Created = DateTime.UtcNow;
            result.Updated = DateTime.UtcNow;
            result.DebugInfo = "Source: QueryConsumer\n";

            evalText = evaluationText.ToString();

            return result;
        }

        ContentType GetContentType(WorkspaceResult result)
        {
            if (!result.ResponseProperties.ContainsKey("content-type")) return ContentType.Unknown;
            return new ContentType(result.ResponseProperties["content-type"]);
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
                    return SanitiseAttribute(title, 128);
            }

            return SanitiseAttribute(title, 128);
        }

        void ProcessMetaTag(HtmlReader reader, WorkspaceResult result)
        {
            var propertyName = reader.GetAttribute("property").ToLower();

            if (!String.IsNullOrWhiteSpace(propertyName))
            {
                propertyName = SanitiseProperty(propertyName);
                var content = SanitiseAttribute(reader.GetAttribute("content"));

                if (!result.PropertyCache.ContainsKey(propertyName))
                    result.PropertyCache.Add(propertyName, reader.GetAttribute("content"));

                switch (propertyName)
                {
                    case "og_title": // og:title
                        if (!result.PropertyCache.ContainsKey("title"))
                            result.PropertyCache.Add("title", content);
                        result.PropertyCache["title"] = content;
                        result.Title = content;
                        break;

                    case "og_description": // og:description
                        result.Description = content;
                        if (!result.PropertyCache.ContainsKey("description"))
                            result.PropertyCache.Add("description", content);
                        result.PropertyCache["description"] = content;
                        break;

                    default:
                        break;
                }
            }
            else
            {
                // other random historical meta tags here
                var metaname = reader.GetAttribute("name").ToLower();

                switch (metaname)
                {
                    case "description":
                        var value = SanitiseAttribute(reader.GetAttribute("content"));
                        if (!result.PropertyCache.ContainsKey("description"))
                            result.PropertyCache.Add("description", value);
                        break;
                }
            }
        }

        string SanitiseAttribute(string content, int maxLength = 1024)
            => WebUtility.HtmlDecode(content.Replace("\n", " ").Replace("\t", " ").Replace("\r", " ").Trim()).Truncate(maxLength);

        // we have to replace ":" as its a reserved char in the filters
        string SanitiseProperty(string propertyName)
            => propertyName.Replace(":", "_").Truncate(128).ToLower();

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
