using System.Linq;
using Fetcho.Common.Entities;
using Fetcho.Common.QueryEngine;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class QueryTest
    {
        [TestInitialize]
        public void Setup()
        {
            var cfg = new FetchoConfiguration();
            FetchoConfiguration.Current = cfg;
            cfg.SetConfigurationSetting(() => cfg.MLModelPath, @"G:\fetcho\data\");
        }

        [TestMethod]
        public void ParseTest()
        {
            const string InputQuery = "ml-model(science,0.98):* has:title has:description lang:en";

            var q = new Query(InputQuery);
            var result = q.Evaluate(null, "words", null);

            Assert.IsTrue(q.OriginalQueryText == InputQuery, q.OriginalQueryText);
            Assert.IsTrue(q.ToString() == InputQuery, q.ToString());
            Assert.IsTrue(result.Action == EvaluationResultAction.Exclude);

        }

        [TestMethod]
        public void Parse2Test()
        {
            const string InputQuery = "uri:reddit.com";

            var q = new Query(InputQuery);
            var result = q.Evaluate(null, "words", null);

            Assert.IsTrue(q.OriginalQueryText == InputQuery, q.OriginalQueryText);
            Assert.IsTrue(q.ToString() == InputQuery, q.ToString());
            Assert.IsTrue(result.Action == EvaluationResultAction.Exclude);
        }

        [TestMethod]
        public void Parse3Test()
        {
            const string InputQuery = "uri:reddit.com";

            var r = new WorkspaceResult
            {
                Uri = InputQuery
            };
 
            var f = new UriFilter(InputQuery);
            Assert.IsTrue(f.IsMatch(r, "", null).Any());
        }

    }
}
