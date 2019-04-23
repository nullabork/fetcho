
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;
using Fetcho.Common.Entities;

namespace Fetcho.Common
{
    public static class RobotsFetcher
    {
        /// <summary>
        /// Download a robots file
        /// </summary>
        /// <param name="robotsUri"></param>
        /// <param name="lastFetched"></param>
        /// <returns></returns>
        public static async Task<RobotsFile> DownloadRobots(Uri anyUri, DateTime? lastFetched)
        {
            RobotsFile robots = null;
            var robotsUri = MakeRobotsUri(anyUri);

            try
            {
                var bb = new BufferBlock<IWebResourceWriter>();

                using (var ms = new MemoryStream())
                {
                    using (var packet = new WebDataPacketWriter(ms))
                    {
                        // this is annoying, I shouldn't have to create a buffer block to get a robots file
                        // or we should put robots into the standard flow of things
                        await bb.SendAsync(packet);
                        await (new HttpResourceFetcher()).Fetch(null, robotsUri, null, lastFetched, bb);
                    }
                    ms.Seek(0, SeekOrigin.Begin);

                    using (var packet = new WebDataPacketReader(CreateXmlReader(ms)))
                    {
                        using (var stream = packet.GetResponseStream())
                        {
                            if (stream == null) robots = new RobotsFile();
                            else robots = new RobotsFile(robotsUri, stream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utility.LogInfo("Fetching {0}:", robotsUri);
                Utility.LogException(ex);
            }

            return robots;
        }

        /// <summary>
        /// Fetch a robot file for a uri
        /// </summary>
        /// <param name="anyUri">Any URI for which you want the robots file for</param>
        /// <returns></returns>
        public static async Task<RobotsFile> GetFile(Uri anyUri)
        {
            //log.Debug("Downloading robots: " + uri);

            Site site = null;
            RobotsFile robotsFile = null;
            var robotsUri = MakeRobotsUri(anyUri);
            bool needsVisiting = true;

            try
            {
                var db = await DatabasePool.GetDatabaseAsync();
                site = await db.GetSite(robotsUri);
                await DatabasePool.GiveBackToPool(db);
                if (site != null)
                {
                    needsVisiting = site.RobotsNeedsVisiting;
                }
                else
                {
                    site = MakeNewSite(anyUri);
                }

                if (needsVisiting)
                {
                    if (site != null && site.IsBlocked)
                    {
                        Utility.LogInfo("Can't get robots file as site is blocked by policy: " + robotsUri);
                        return null;
                    }

                    robotsFile = await DownloadRobots(robotsUri, site.LastRobotsFetched);
                    site.LastRobotsFetched = DateTime.UtcNow;
                    site.RobotsFile = robotsFile;
                    db = await DatabasePool.GetDatabaseAsync();
                    await db.SaveSite(site);
                    await DatabasePool.GiveBackToPool(db);
                }
                else
                    robotsFile = site.RobotsFile;

            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
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
        public static Uri MakeRobotsUri(Uri anyUri)
        {
            if (!anyUri.IsAbsoluteUri)
                throw new FetchoException("Needs to be an absolute URI");

            string newUri = string.Format("{0}://{1}{2}/robots.txt", anyUri.Scheme, anyUri.Host, (anyUri.IsDefaultPort ? "" : ":" + anyUri.Port));

            return new Uri(newUri);
        }



        private static XmlWriter CreateXmlWriter(Stream stream) =>
            XmlWriter.Create(stream, new XmlWriterSettings() { ConformanceLevel = ConformanceLevel.Fragment });

        private static XmlReader CreateXmlReader(Stream stream) =>
            XmlReader.Create(stream, new XmlReaderSettings() { ConformanceLevel = ConformanceLevel.Fragment });

    }

}
