using System.Linq;
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
            cfg.SetConfigurationSetting(() => cfg.DataSourcePath, @"G:\fetcho\data\");
        }

        [TestMethod]
        public void MachineLearningModelBuilderTest()
        {
            const string InputQuery = "ml-model(science,0.98):*";

            var f = MachineLearningModelFilter.Parse(InputQuery);
            Assert.IsNotNull(f);
            Assert.IsTrue(f.GetQueryText() == InputQuery, f.GetQueryText());

            var eval = f.IsMatch(null, "Test", null);
            Assert.IsTrue(eval.Any());
        }

    }
}
