using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
    public class BaseTestClass
    {
        public void SetupBasicConfiguration()
        {
            FetchoConfiguration.Current = new FetchoConfiguration();
        }
    }
}
