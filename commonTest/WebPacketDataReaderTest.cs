using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class WebPacketDataReaderTest
    {

        const string teststring1 = "Uri: https://www.ign.com/articles/2016/05/10/halo-5s-infection-mode-detailed\nResponseTime: 00:00:00.3426005\n" +
            "Date: 26/02/2019 8:18:46 PM\nUser-Agent: ResearchBot 0.1\nReferer: https://en.wikipedia.org/wiki/Flood_(Halo)\nHost: www.ign.com\n"+
            "Accept-Encoding: gzip, deflate\nConnection: Close\n";

        [TestMethod]
        public void GetContentTypeFromResponseHeadersTest()
        {
            var ct = WebDataPacketReader.GetContentTypeFromResponseHeaders("content-type:");
            Assert.IsTrue(ct == ContentType.Unknown, ct.ToString());
            Assert.IsTrue(ContentType.IsUnknownOrNull(ct), ct.ToString());

            ct = WebDataPacketReader.GetContentTypeFromResponseHeaders("");
            Assert.IsTrue(ct == ContentType.Unknown, ct.ToString());
            Assert.IsTrue(ContentType.IsUnknownOrNull(ct), ct.ToString());

            ct = WebDataPacketReader.GetContentTypeFromResponseHeaders("Content-Type: text/html");
            Assert.IsTrue(ct.Raw == "text/html");
        }

        [TestMethod]
        public void GetRefererUriFromRequestStringTest()
        {
            var referer = WebDataPacketReader.GetRefererUriFromRequestString(teststring1);
            Assert.IsNotNull(referer);
            Assert.IsTrue(referer.ToString() == "https://en.wikipedia.org/wiki/Flood_(Halo)", referer.ToString());
        }
    }
}
