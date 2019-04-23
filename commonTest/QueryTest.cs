using System;
using System.Linq;
using System.Reflection;
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


        [TestMethod]
        public void Parse4Test()
        {
            const string InputQuery = "site:reddit.com OR site:wikipedia.org OR site:news.com.au";

            var r1 = new WorkspaceResult() { Uri = "https://www.reddit.com" };
            var r2 = new WorkspaceResult() { Uri = "https://en.wikipedia.org" };
            var r3 = new WorkspaceResult() { Uri = "https://www.news.com.au" };
            var r4 = new WorkspaceResult() { Uri = "https://www.example.com.au" };

            var q = new Query(InputQuery);
            Assert.IsTrue(q.Evaluate(r1, null, null).Action == EvaluationResultAction.Include);
            Assert.IsTrue(q.Evaluate(r2, null, null).Action == EvaluationResultAction.Include);
            Assert.IsTrue(q.Evaluate(r3, null, null).Action == EvaluationResultAction.Include);
            Assert.IsFalse(q.Evaluate(r4, null, null).Action == EvaluationResultAction.Include);
        }

        /// <summary>
        /// Tests that a bogus query throws an argument exception
        /// </summary>
        [TestMethod]
        public void Parse5Test()
        {
            const string ExampleQuery = "has:title has:description lang:en response-header(content-type):text/html distinct-window(domain):1000 property(og_type):article regex:(((?:[a-zA-Z,._\\-\"']+\\s){3,3}(?:coffee)(?:\\s[a-zA-Z,._\\-\"']+){3,3}):((?:[a-zA-Z,._\\-\"']+\\s){3,3}(?:coffee)(?:\\s[a-zA-Z,._\\-\"']+){3,3})";
            Assert.ThrowsException<ArgumentException>(() =>
            {
                var q = new Query(ExampleQuery);
            });

        }
    }
}
