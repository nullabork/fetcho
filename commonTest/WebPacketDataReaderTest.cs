using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class WebPacketDataReaderTest
    {

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
    }
}
