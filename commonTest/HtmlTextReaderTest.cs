using Fetcho.ContentReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace commonTest
{
    [TestClass]
    public class HtmlTextReaderTest
    {
        [TestMethod]
        public void TestRead()
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes("somemoretext<title>some text</title>"));
            var r = new HtmlTextReader(ms);

            while (r.Peek() != -1) Assert.IsTrue((char)r.Read() != '<');

            ms = new MemoryStream(Encoding.UTF8.GetBytes("somemoretext<title>some text</title>"));
            r = new HtmlTextReader(ms);
            string line = r.ReadToEnd();
            Assert.IsTrue(line == "some text", "Line is: " + line);

            ms = new MemoryStream(Encoding.UTF8.GetBytes("somemoretext<title>some text</title>"));
            r = new HtmlTextReader(ms);
            line = r.ReadLine();
            Assert.IsTrue(line == "some text", "Line is: " + line);
        }
    }
}
