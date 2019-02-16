using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class ContentTypeTest
    {
        const string MalformedContentType = "text";
        const string MalformedContentType2 = "/html";
        const string TextHtmlSimple = "text/html";
        const string TextHtmlComplex = "text/html; charset=UTF-8";

        [TestMethod]
        public void ConstructorTest()
        {

            var ct = new ContentType(TextHtmlSimple);
            Assert.IsTrue(ct.MediaType == "text");
            Assert.IsTrue(ct.SubType == "html");

            ct = new ContentType(TextHtmlComplex);
            Assert.IsTrue(ct.MediaType == "text");
            Assert.IsTrue(ct.SubType == "html");
            Assert.IsTrue(ct.Attributes[0].Key == "charset");
            Assert.IsTrue(ct.Attributes[0].Value == "UTF-8");

            Assert.IsTrue(ct.ToString() == TextHtmlComplex);

            ct = new ContentType(MalformedContentType);
            Assert.IsTrue(ct.MediaType == MalformedContentType, ct.MediaType);
            Assert.IsTrue(String.IsNullOrWhiteSpace(ct.SubType), ct.SubType);
            Assert.IsTrue(ct.Raw == MalformedContentType, ct.Raw);
            Assert.IsFalse(ct.IsBlank);
            Assert.IsFalse(ct == ContentType.Unknown);

            ct = new ContentType(MalformedContentType2);
            Assert.IsTrue(ct.SubType == "html", ct.SubType);
            Assert.IsTrue(String.IsNullOrWhiteSpace(ct.MediaType), ct.MediaType);
            Assert.IsTrue(ct.Raw == MalformedContentType2, ct.Raw);
            Assert.IsFalse(ct.IsBlank);
            Assert.IsFalse(ct == ContentType.Unknown);
        }

        [TestMethod]
        public void GuessTest()
        {
            byte[] htmlbytes = System.Text.Encoding.ASCII.GetBytes("<!DOCTYPE html > <html prefix=\"og: http://ogp.me/ns#\"> <head> <title>Best String to Hex Converter Online to Convert Text to Hex.</title> <meta http-equiv=\"content-language\" content=\"en-US\"> <meta http-equiv=\"Content-Type\" content=\"text/html;charset=utf-8\" /> <link href=\"/img/cb.png\" rel=\"icon\" /> <meta property=\"fb:app_id\" content=\"\" /> <meta property=\"og:url\" content=\"https://codebeautify.org/string-hex-converter\" /> <meta name=\"description\" content=\"Convert String to Hex (Text to Hex) Online and Save and Share. String to Hexadecimal\" />");

            Assert.IsTrue(ContentType.Guess((byte[])null) == ContentType.Unknown);

            var ct = ContentType.Guess(new byte[] { });
            Assert.IsTrue(ct.Raw == String.Empty);
            Assert.IsTrue(ContentType.Unknown.Raw == String.Empty);
            Assert.IsTrue(ct.Raw == ContentType.Unknown.Raw, "'{0}'", ct);

            var texthtml = ContentType.Guess(htmlbytes);
            Assert.IsTrue(texthtml.MediaType == "text");
            Assert.IsTrue(texthtml.SubType == "html");
        }
    }
}
