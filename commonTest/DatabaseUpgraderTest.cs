using Fetcho.Common.Dbup;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class DatabaseUpgraderTest : BaseTestClass
    {
        [TestMethod]
        public void UpgradeTest()
        {
            DatabaseUpgrader.Upgrade();
        }
    }
}
