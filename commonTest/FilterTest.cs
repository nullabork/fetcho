using System;
using System.IO;
using System.Linq;
using Fetcho.ContentReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class FilterTest
    {
        [TestInitialize]
        public void Setup()
        {
            var cfg = new FetchoConfiguration();
            FetchoConfiguration.Current = cfg;
            cfg.SetConfigurationSetting(() => cfg.MLModelPath, @"G:\fetcho\data\");
        }

        [TestMethod]
        public void MachineLearningModelBuilderTest()
        {
            const string InputQuery = "ml-model(science,0.98):*";

            var f = MachineLearningModelFilter.Parse(InputQuery, 0);
            Assert.IsNotNull(f);
            Assert.IsTrue(f.GetQueryText() == InputQuery, f.GetQueryText());

            var eval = f.IsMatch(null, "Test", null);
            Assert.IsTrue(eval.Any());
        }

        [TestMethod]
        public void RegexSlowTest()
        {
            const string RegexSlowTestFilePath = @"testdata\FilterTest\RegexSlowTest.html";
            const string ExampleToken = @"regex:(\s*\w*\s*){2,6}\scoffee\s(\s*\w*\s*){2,6}:(\s*\w*\s*){2,6}\scoffee\s(\s*\w*\s*){2,6}";
            var f = Filter.CreateFilter(ExampleToken, 0);

            Assert.IsInstanceOfType(f, typeof(RegexFilter));

            var regex = f as RegexFilter;
            Assert.IsTrue(regex.RegexPattern == ExampleToken.Substring(6));

            var fragment = BracketPipeTextExtractor.ReadAllText(File.Open(RegexSlowTestFilePath, FileMode.Open));
            var tags = regex.IsMatch(null, fragment.Aggregate("", (x, y) => x + " " + y), null);

            foreach (var tag in tags) 
                Console.WriteLine(tags);

        }

    }
}
