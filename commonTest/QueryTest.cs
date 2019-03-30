using System;
using System.Reflection;
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
            try
            {
                Filter.InitaliseFilterTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine(ex.LoaderExceptions[0]);
            }
            var cfg = new FetchoConfiguration();
            FetchoConfiguration.Current = cfg;
            cfg.SetConfigurationSetting(() => cfg.DataSourcePath, @"G:\fetcho\data\");
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

    }
}
