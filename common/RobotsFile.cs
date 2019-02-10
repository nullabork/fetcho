
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Fetcho.Common.entities;
using log4net;

namespace Fetcho.Common
{
    [Serializable]
    public class RobotsFile : IDisposable
    {
        static readonly ILog log = LogManager.GetLogger(typeof(RobotsFile));

        public const string DefaultUserAgent = "*";

        /// <summary>
        /// True if this file is bad
        /// </summary>
        public bool Malformed { get; set; }

        /// <summary>
        /// URI for this robots file
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// A list of all disallowed URIs
        /// </summary>
        public FiniteStateMachine<FiniteStateMachine<FiniteStateMachineBooleanState, char>, char> Disallow { get; protected set; }

        /// <summary>
        /// A list of all allowed URIs
        /// </summary>
        public FiniteStateMachine<FiniteStateMachine<FiniteStateMachineBooleanState, char>, char> Allow { get; protected set; }

        /// <summary>
        /// A list of site maps referenced by this robots file
        /// </summary>
        public List<string> SiteMaps { get; set; }

        public RobotsFile()
        {
            Disallow = new FiniteStateMachine<FiniteStateMachine<FiniteStateMachineBooleanState, char>, char>();
            Allow = new FiniteStateMachine<FiniteStateMachine<FiniteStateMachineBooleanState, char>, char>();
            SiteMaps = new List<string>();
            Malformed = false;
        }

        public RobotsFile(byte[] data) : this(XmlReader.Create(new MemoryStream(data)))
        {
        }

        public RobotsFile(XmlReader reader) : this()
        {
            using (reader)
            {
                Process(reader);
            }
        }

        /// <summary>
        /// Returns true if the uri is not disallowed to be crawled
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="userAgent"></param>
        /// <returns></returns>
        public bool IsNotDisallowed(Uri uri, string userAgent = Settings.UserAgent) => !IsDisallowed(uri, userAgent);

        /// <summary>
        /// Returns true if the uri is not allowed to be crawled
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public bool IsDisallowed(Uri uri, string userAgent = Settings.UserAgent)
        {
            bool rtn = false;
            if (String.IsNullOrWhiteSpace(userAgent))
                userAgent = Settings.UserAgent;

            FiniteStateMachine<FiniteStateMachineBooleanState, char>[] matcher = Disallow.GetState(userAgent);
            FiniteStateMachineBooleanState[] states = null;
            if (matcher.Length == 0)
                matcher = Disallow.RootNode.State.ToArray();

            // occurs when there's no robots file!
            if (matcher.Length == 0)
                rtn = false;
            else
            {
                states = matcher[0].GetState(uri.AbsolutePath.ToString());

                if (states == null)
                    rtn = false;
                else if (states.Length == 0)
                    rtn = false;
                else if (states.Length > 1)
                {
                    log.Error("More than one state for Robots URI" + uri);
                    rtn = false;
                }
                else if (states[0] == FiniteStateMachineBooleanState.Accept)
                    rtn = true;
                else
                    rtn = false;
            }

            return rtn;
        }

        /// <summary>
        /// Process the file into local memory
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private void Process(XmlReader xmlreader)
        {
            string userAgent = "";

            // add the defaults
            addStringToMatcher(Disallow,
                               "*",
                               new FiniteStateMachine<FiniteStateMachineBooleanState, char>()
                              );
            addStringToMatcher(Allow,
                               "*",
                               new FiniteStateMachine<FiniteStateMachineBooleanState, char>()
                              );

            using (var packet = new WebDataPacketReader(xmlreader))
            {
                var response = packet.GetResponseStream();
                if (response == null) return; // no response

                using (var reader = new StreamReader(response))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine().ToLower().Trim();

                        // skip comments
                        if (line.StartsWith("#", StringComparison.InvariantCultureIgnoreCase))
                            continue;

                        if (line.StartsWith("user-agent:", StringComparison.InvariantCultureIgnoreCase))
                        {
                            userAgent = "";
                            if (line.Length > 11)
                                userAgent = line.Substring(11, line.Length - 11).Trim();

                            if (userAgent == Settings.UserAgent)
                                log.Error(this.Uri + " has a specific restriction for our user-agent");

                            addStringToMatcher(Disallow,
                                               userAgent,
                                               new FiniteStateMachine<FiniteStateMachineBooleanState, char>()
                                              );
                            addStringToMatcher(Allow,
                                               userAgent,
                                               new FiniteStateMachine<FiniteStateMachineBooleanState, char>()
                                              );
                        }
                        else
                        {
                            if (line.EndsWith("*", StringComparison.InvariantCultureIgnoreCase)) line = line.Substring(0, line.Length - 1); // chop it

                            if (line.StartsWith("disallow:", StringComparison.InvariantCultureIgnoreCase))
                            {
                                var disallow_matcher = Disallow.GetState(userAgent);
                                if (disallow_matcher.Length == 0)
                                    throw new Exception("No default disallow matcher available for '" + userAgent + "' uri " + Uri);

                                if (line.Length > 9)
                                    addStringToMatcher(disallow_matcher[0],
                                                       line.Substring(9, line.Length - 9).Trim(),
                                                       FiniteStateMachineBooleanState.Accept);
                            }
                            else if (line.StartsWith("allow:", StringComparison.InvariantCultureIgnoreCase))
                            {
                                var allow_matcher = Allow.GetState(userAgent);
                                if (allow_matcher.Length == 0)
                                    throw new Exception("No default allow matcher available for '" + userAgent + "' uri " + Uri);

                                if (line.Length > 6)
                                    addStringToMatcher(allow_matcher[0],
                                                       line.Substring(6, line.Length - 6).Trim(),
                                                       FiniteStateMachineBooleanState.Accept);
                            }
                            else if (line.StartsWith("sitemap:", StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (line.Length > 8)
                                    SiteMaps.Add(line.Substring(8, line.Length - 8).Trim());
                            }
                        }
                    }
                }
            }
        }

        private void addStringToMatcher<StateType>(FiniteStateMachine<StateType, char> stateMachine,
                                                   string matchString,
                                                   StateType state)
        {
            var current_node = stateMachine.RootNode;
            var default_transition = DefaultTransitionPath.Self;
            var current_state = state;

            if ((matchString == "*" || matchString == "") &&
                !state.Equals(default(StateType)))
            {
                stateMachine.RootNode.State.Add(state);
                return;
            }

            for (int i = 0; i < matchString.Length; i++)
            {
                bool skip_next = true;
                bool skip_current = false;
                current_state = default(StateType);

                if (matchString[i] == '*')
                    skip_current = true;

                if (i < matchString.Length - 1)
                {
                    if (matchString[i + 1] == '$')
                        default_transition = DefaultTransitionPath.RootNode;
                    else if (matchString[i + 1] != '*')
                        skip_next = false;
                }
                else if ( i == matchString.Length - 1)
                    current_state = state;

                if (skip_next && matchString.Length - 2 == i)
                    current_state = state;

                if (!skip_current)
                    current_node = stateMachine.AddStateInput(current_node,
                                                              current_state,
                                                              matchString[i],
                                                              default_transition);

                if (skip_next)
                    i++;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Uri:\t{0}\n", Uri);
            sb.AppendFormat("Malformed:\t{0}\n", Malformed);
            sb.AppendFormat("SiteMaps:\t{0}\n", String.Join("\n\t", SiteMaps.ToArray()));
            sb.AppendFormat("Disallow:\t{0}\n", Disallow);
            sb.AppendFormat("Allow:\t{0}\n", Allow);
            return sb.ToString();
        }

        /// <summary>
        /// Fetch a robot file for a uri
        /// </summary>
        /// <param name="uri">Any URI for which you want the robots file for</param>
        /// <returns></returns>
        public static async Task<RobotsFile> GetFile(Uri uri)
        {
            //log.Debug("Downloading robots: " + uri);

            Site site = null;
            RobotsFile robotsFile = null;
            var robotsUri = MakeRobotsUri(uri);

            try
            {

                bool needsVisiting = true;

                using (var db = new Database())
                {
                    site = await db.GetSite(robotsUri);
                    if (site != null)
                        needsVisiting = site.RobotsNeedsVisiting;
                    else
                        site = MakeNewSite(uri);
                }

                if (needsVisiting)
                {
                    log.InfoFormat("Robots {0} needs visiting", robotsUri);

                    if (site != null && site.IsBlocked)
                    {
                        log.Error("Can't get robots file as site is blocked by policy: " + robotsUri);
                        return null;
                    }

                    robotsFile = await DownloadRobots(robotsUri, site.LastRobotsFetched);
                    site.LastRobotsFetched = DateTime.Now;
                    site.RobotsFile = robotsFile;
                    using (var db = new Database())
                        await db.SaveSite(site);
                }
                else
                    robotsFile = site.RobotsFile;

            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return robotsFile;
        }

        /// <summary>
        /// Make a new site object from any uri
        /// </summary>
        /// <param name="anyUri"></param>
        /// <returns></returns>
        static Site MakeNewSite(Uri anyUri) => new Site(anyUri);

        /// <summary>
        /// Get the robots URI for any URI
        /// </summary>
        /// <param name="anyUri"></param>
        /// <returns></returns>
        static Uri MakeRobotsUri(Uri anyUri)
        {
            if (!anyUri.IsAbsoluteUri)
                throw new FetchoException("Needs to be an absolute URI");

            string newUri = string.Format("{0}://{1}{2}/robots.txt", anyUri.Scheme, anyUri.Host, (anyUri.IsDefaultPort ? "" : ":" + anyUri.Port));

            return new Uri(newUri);
        }

        /// <summary>
        /// Download a robots file
        /// </summary>
        /// <param name="robotsUri"></param>
        /// <param name="lastFetched"></param>
        /// <returns></returns>
        public static async Task<RobotsFile> DownloadRobots(Uri robotsUri, DateTime? lastFetched)
        {
            RobotsFile robots = null;

            try
            {
                IPAddress ipAddress = await Utility.GetHostIPAddress(robotsUri);

                if (ipAddress == IPAddress.None)
                {
                    log.InfoFormat("Can't find IP Address for {0}", robotsUri);
                    return null;
                }

                using (var ms = new MemoryStream())
                using (var writer = XmlWriter.Create(ms))
                {
                    await (new HttpResourceFetcher()).Fetch(null, robotsUri, writer, NullBlockProvider.Default, lastFetched);
                    ms.Seek(0, SeekOrigin.Begin);
                    robots = new RobotsFile(XmlReader.Create(ms, new XmlReaderSettings()
                    { ConformanceLevel = ConformanceLevel.Fragment }));
                }
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Fetching {0}:", robotsUri);
                log.Error(ex);
            }

            return robots;
        }

        /// <summary>
        /// Dispose the robots file
        /// </summary>
        protected virtual void Dispose(bool disposable)
        {
            foreach (var state in Allow.States.Keys)
            {
                state.Dispose();
            }

            foreach (var state in Disallow.States.Keys)
            {
                state.Dispose();
            }

            Disallow.Dispose();
            Allow.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
