using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class UtilityTest
    {
        public string SomePropertyName1 { get; set; }

        public string SomePropertyName2;

        public static string SomePropertyName3;

        [TestMethod]
        public void GetPropertyNameTest()
        {
            var u = new UtilityTest();

            Assert.IsTrue(Utility.GetPropertyName(() => u.SomePropertyName1) == "SomePropertyName1");
            Assert.IsTrue(Utility.GetPropertyName(() => u.SomePropertyName2) == "SomePropertyName2");
            Assert.IsTrue(Utility.GetPropertyName(() => UtilityTest.SomePropertyName3) == "SomePropertyName3");
        }
    }
}
