
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using log4net;

namespace Fetcho.Common
{

    public static class Utility
    {
        static readonly ILog log = LogManager.GetLogger(typeof(Utility));

        public static async Task<IPAddress> GetHostIPAddress(Uri uri)
        {
            IPAddress addr = IPAddress.None;
            try
            {

                // if its already an ip address!
                if (IPAddress.TryParse(uri.Host, out addr))
                    return addr;

                // else lets try and find it via the DNS lookup
                var hostEntry = await Dns.GetHostEntryAsync(uri.Host);
                if (hostEntry.AddressList.Length > 0)
                {
                    addr = hostEntry.AddressList[0];
                    return addr;
                }
                else
                {
                    addr = IPAddress.None;
                    return addr;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                addr = IPAddress.None;
                return addr;
            }
        }

        /// <summary>
        /// From a potential string extract the URIs within it
        /// </summary>
        /// <param name="sourceUri">Where the URI comes from for relative URIs. If null is passed only absolute URLs will be considered</param>
        /// <param name="uriCandidate">A potential URI</param>
        /// <returns>An enumerable collection of URIs</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "1#")]
        public static IEnumerable<Uri> GetLinks(Uri sourceUri, string uriCandidate)
        {
            var list = new List<Uri>();

            try
            {
                string tempUrl = uriCandidate;

                // can't follow a JS link
                if (tempUrl.StartsWith("javascript:"))
                    return list;

                // drop mailto links they're emails
                if (tempUrl.StartsWith("mailto:", StringComparison.InvariantCultureIgnoreCase))
                    return list;

                // drop file links they go no where
                if (tempUrl.StartsWith("file:", StringComparison.InvariantCultureIgnoreCase))
                    return list;

                // can't follow an internal anchor
                if (tempUrl.StartsWith("#", StringComparison.InvariantCultureIgnoreCase))
                    return list;

                // remove # anchors from links
                if (tempUrl.IndexOf("#", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    tempUrl = tempUrl.Substring(0, tempUrl.IndexOf("#", StringComparison.InvariantCultureIgnoreCase));

                // clean up the shorthand
                if (tempUrl.StartsWith("//", StringComparison.InvariantCultureIgnoreCase))
                    tempUrl = sourceUri.Scheme + ":" + tempUrl;

                // wierd triple slash on id.wikipedia.org
                if (tempUrl.StartsWith("http:///", StringComparison.InvariantCultureIgnoreCase))
                    tempUrl = tempUrl.Replace("http:///", "http://");

                // wierd triple slash on id.wikipedia.org
                if (tempUrl.StartsWith("https:///", StringComparison.InvariantCultureIgnoreCase))
                    tempUrl = tempUrl.Replace("https:///", "https://");

                // if fragment build an absolute URL
                if (tempUrl.IndexOf("://", StringComparison.InvariantCultureIgnoreCase) < 0)
                {
                    // if absolute url
                    if (tempUrl.StartsWith("/", StringComparison.InvariantCultureIgnoreCase) || tempUrl.StartsWith("\\", StringComparison.InvariantCultureIgnoreCase))
                    {
                        tempUrl = string.Format("{0}://{1}{2}", sourceUri.Scheme, sourceUri.Host, tempUrl);
                    }
                    // if relative url
                    else if (sourceUri != null)
                    {
                        // chop off the page
                        int index = sourceUri.ToString().LastIndexOfAny(new char[] { '\\', '/' });
                        if (index >= 0)
                            tempUrl = sourceUri.ToString().Substring(0, index) + "/" + tempUrl;
                        else // why would the index be neg 1?
                        {
                            log.ErrorFormat("GetLinks: Bogus link can't find '\' or '/' in sourceUri: GetLink( sourceUri: {0}, url: {1} );", sourceUri, uriCandidate);
                            return list; // no log bogus link
                        }
                    }
                }

                if (Uri.IsWellFormedUriString(tempUrl, UriKind.Absolute))
                {
                    var uri = new Uri(tempUrl);
                    list.Add(uri);
                    list.AddRange(GetSubLinks(uri));
                }

            }
            catch (UriFormatException ex)
            {
                log.ErrorFormat("LogLink( links, {0}, {1} ) -> {2}", sourceUri, uriCandidate, ex.Message);
            }

            return list;
        }

        private static IEnumerable<Uri> GetSubLinks(Uri uri)
        {
            var list = new List<Uri>();
            int index = 1;
            int last_index = 1;

            while (last_index > 0)
            {
                index = uri.ToString().IndexOf("http://", last_index, StringComparison.InvariantCultureIgnoreCase);
                if (index < 0)
                    index = uri.ToString().IndexOf("https://", last_index, StringComparison.InvariantCultureIgnoreCase);

                if (index > 0)
                    try
                    {
                        list.Add(new Uri(uri.ToString().Substring(index)));
                    }
                    catch (UriFormatException ex)
                    {
                        log.Error("GetSubLinks - failed to log link " + uri.ToString().Substring(index), ex);
                    }

                last_index = index + 1;
            }

            return list;
        }


    }
}
