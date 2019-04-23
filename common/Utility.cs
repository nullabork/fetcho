
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using log4net;

namespace Fetcho.Common
{

    public static class Utility
    {
        static readonly ILog log = LogManager.GetLogger(typeof(Utility));
        static Random random = new Random(DateTime.UtcNow.Millisecond);

        /// <summary>
        /// Get the IP Address of the host from a URI
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static async Task<IPAddress> GetHostIPAddress(Uri uri, bool deterministic = true)
        {
            IPAddress addr = IPAddress.None;
            try
            {

                // if its already an ip address!
                if (IPAddress.TryParse(uri.Host, out addr))
                    return addr;

                // else lets try and find it via the DNS lookup
                var addresses = await Dns.GetHostAddressesAsync(uri.Host);
                if (addresses.Length > 0)
                {
                    // IPV4, Order always
                    // this is a bit hacky because we're ordering and getting the first to support
                    // sorting and queuing elsewhere.
                    if (deterministic)
                        addr = addresses
                            .Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                            .OrderBy(x => BitConverter.ToInt32(x.GetAddressBytes().Reverse().ToArray(), 0))
                            .FirstOrDefault() ?? IPAddress.None;
                    else
                        addr = addresses
                            .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork) ?? IPAddress.None;

                    return addr;
                }
                else
                {
                    addr = IPAddress.None;
                    return addr;
                }
            }
            catch (SocketException)
            {
                // ignore it, host lookup probs
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

            addr = IPAddress.None;
            return addr;
        }

        /// <summary>
        /// Creates a new file with a name that is indexed from the filename if it already exists
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string CreateNewFileOrIndexNameIfExists(string fileName)
        {
            if (File.Exists(fileName))
            {
                string path = Path.GetDirectoryName(fileName);
                string name = Path.GetFileNameWithoutExtension(fileName);
                string extension = Path.GetExtension(fileName);

                int index = 0;
                do
                {
                    fileName = Path.Combine(path, String.Format("{0}-{1}{2}", name, index, extension));
                    index++;
                }
                while (File.Exists(fileName));
            }

            File.Create(fileName).Dispose();

            return fileName;
        }

        /// <summary>
        /// Use GZip to compress a file
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destPath"></param>
        public static void GZipCompressToFilePath(string sourcePath, string destPath)
        {
            using (var sf = new FileStream(sourcePath, FileMode.Open))
            {
                using (var gzip = new GZipStream(new FileStream(destPath, FileMode.Create),
                                                  CompressionLevel.Optimal,
                                                  false))
                {
                    sf.CopyTo(gzip);
                }
            }
        }

        /// <summary>
        /// Decompress a GZip file
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destPath"></param>
        public static void GZipDecompressToFilePath(string sourcePath, string destPath)
        {
            using (var gzip = new GZipStream(new FileStream(sourcePath, FileMode.Open), CompressionMode.Decompress, false))
            {
                using (var sf = new FileStream(destPath, FileMode.OpenOrCreate))
                {
                    gzip.CopyTo(sf);
                }
            }
        }

        private static readonly byte[] GZipHeaderBytes = { 0x1f, 0x8b, 8, 0, 0, 0, 0, 0, 4, 0 };
        private static readonly byte[] GZipLevel10HeaderBytes = { 0x1f, 0x8b, 8, 0, 0, 0, 0, 0, 2, 0 };

        // not sure what this is but GZipStream is now producing it. Reading the docs they indicate they switched to 
        // zlib library in .NET 4.5 so it could be a new compression algorithm
        private static readonly byte[] GZipHeaderBytes2 = { 0x1f, 0x3f, 8, 0, 0, 0, 0, 0, 4, 0 };

        /// <summary>
        /// Attempts to detect if the current file is a GZip file
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static bool IsPossibleGZippedFile(string file)
        {
            using (Stream stm = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] buffer = new byte[10];
                if (stm.Read(buffer, 0, 10) < 10)
                    return false;
                else
                    return IsPossibleGZippedBytes(buffer);
            }
        }

        /// <summary>
        /// Detects whether the byte string passed represents a GZip file header
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static bool IsPossibleGZippedBytes(byte[] bytes)
        {
            var yes = bytes.Length >= 10;

            if (!yes)
                return false;

            var header = new ArraySegment<Byte>(bytes, 0, 10);
            byte[] a = header.Array;

            return a.SequenceEqual(GZipHeaderBytes2) || a.SequenceEqual(GZipHeaderBytes) || a.SequenceEqual(GZipLevel10HeaderBytes);
        }

        /// <summary>
        /// Given a file it will pass out a stream that reads the file decompressed whether its compressed or not
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        public static Stream GetDecompressedStream(string filepath, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            // in theory this should cache the file so that when we open it a second time it effectively is one call
            bool isZipped = IsPossibleGZippedFile(filepath);
            var stm = new FileStream(filepath, fileMode, fileAccess, fileShare);

            if (isZipped)
                return new GZipStream(stm, CompressionMode.Decompress, false);
            return stm;
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
            string[] acceptedSchemes = new string[] { "http", "https" };
            const int MaxUriLength = 2043; // IIS limit seems to be the shortest. Lets be serious, if anyone uses anything longer than this...
            var list = new List<Uri>(5);

            if (sourceUri == null)
                return list;

            try
            {
                string tempUrl = uriCandidate.Trim();

                // too long 
                if (tempUrl.Length > MaxUriLength)
                    return list;

                // test for http/https and chuck out all the others
                int schemeIndex = tempUrl.IndexOf(":");
                if (schemeIndex > -1)
                {
                    string scheme = tempUrl.Substring(0, schemeIndex).ToLower();
                    if (scheme.All(Char.IsLetter) && !acceptedSchemes.Any(x => scheme == x))
                    {
                        //log.DebugFormat("Unaccepted scheme detected: {0}", tempUrl);
                        return list;
                    }
                }

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
                        tempUrl = string.Format("{0}://{1}{2}", sourceUri?.Scheme, sourceUri?.Host, tempUrl);
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

                tempUrl = tempUrl.CleanupForXml();

                if (Uri.IsWellFormedUriString(tempUrl, UriKind.Absolute) &&
                    tempUrl.Contains(".") &&
                    tempUrl.Length < MaxUriLength)
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
                        string tempUri = uri.ToString().Substring(index);
                        if (Uri.IsWellFormedUriString(tempUri, UriKind.Absolute) && tempUri.Contains("."))
                            list.Add(new Uri(tempUri));
                    }
                    catch (UriFormatException ex)
                    {
                        log.Error("GetSubLinks - failed to log link " + uri.ToString().Substring(index), ex);
                    }

                last_index = index + 1;
            }

            return list;
        }

        public static string GetRandomHashString() => MD5Hash.Compute(random.Next(int.MinValue, int.MaxValue)).ToString();

        public static void LogException(Exception ex)
        {
            if ( ex is ReflectionTypeLoadException rtlex )
            {
                log.Error(rtlex.LoaderExceptions[0]);
            }
            else
            {
                System.Diagnostics.Debug.Write(ex);
                log.Error(ex);
            }
        }

        public static void LogInfo(string format, params object[] args) => log.InfoFormat(format, args);

        // <summary>
        // Get the name of a static or instance property from a property access lambda.
        // </summary>
        // <typeparam name="T">Type of the property</typeparam>
        // <param name="propertyLambda">lambda expression of the form: '() => Class.Property' or '() => object.Property'</param>
        // <returns>The name of the property</returns>
        public static string GetPropertyName<T>(Expression<Func<T>> propertyLambda)
        {
            if (!(propertyLambda.Body is MemberExpression me))
            {
                throw new ArgumentException("You must pass a lambda of the form: '() => Class.Property' or '() => object.Property'");
            }

            return me.Member.Name;
        }

        public static T TryParse<T>(string inValue)
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));

            return (T)converter.ConvertFromString(null, CultureInfo.InvariantCulture, inValue);
        }

        public static string MakeTag(string token)
            => token?.Trim().Replace(' ', '_');

        public static IEnumerable<string> MakeTags(params string[] tokens)
            => tokens.Select(MakeTag);

        public static IEnumerable<string> MakeTags(IEnumerable<string> tokens)
            => tokens.Select(MakeTag);

    }
}
