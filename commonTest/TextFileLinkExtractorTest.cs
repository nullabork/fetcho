using Fetcho.ContentReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class TextFileLinkExtractorTest
    {
        [TestMethod]
        public void ExtractTest()
        {
            var bogusUri = new Uri("http://www.blah.com");
            var test1 = "https://www.test.com/";
            var test1expected = test1;
            var test2 = "<a href=\"https://www.blahblah.com\">blah blah</a>";
            var test2expected = "https://www.blahblah.com/";
            var test3 = "<a href='https://www.blahblah.com'>blah blah</a>";
            var test3expected = "https://www.blahblah.com/";

            var u = GetUri(bogusUri, test1);
            Assert.IsTrue(u.ToString() == test1expected, u.ToString());
            u = GetUri(bogusUri, test2);
            Assert.IsTrue(u.ToString() == test2expected, u.ToString());
            u = GetUri(bogusUri, test3);
            Assert.IsTrue(u.ToString() == test3expected, u.ToString());

        }

        private Uri GetUri(Uri bogusCurrentUri, string fragmentToTest)
        {
            using (var r = new StreamReader(new MemoryStream(System.Text.Encoding.ASCII.GetBytes(fragmentToTest))))
            {
                var t = new TextFileLinkExtractor(bogusCurrentUri, r);
                return  t.NextUri();
            }
        }

    }
}
