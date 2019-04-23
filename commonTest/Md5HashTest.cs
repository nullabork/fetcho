using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fetcho.Common.Tests
{
    [TestClass]
	public class Md5HashTest
	{
	  [TestMethod]
	  public void EqualsTest()
	  {
#pragma warning disable CS1718 // Comparison made to same variable
            Assert.IsTrue(MD5Hash.MinValue == MD5Hash.MinValue );
#pragma warning restore CS1718 // Comparison made to same variable
            Assert.IsTrue(new MD5Hash("12345678123456781234567812345678") == new MD5Hash("12345678123456781234567812345678") );
	  }
	  
	  [TestMethod]
	  public void ComputeTest()
	  {
	    var h1 = MD5Hash.Parse("0B1DBE29DBED4AFC1196CC430FD370C1");
	    var h2 = MD5Hash.Compute("http://en.wikipedia.org/robots.txt");
	    
	    Assert.IsTrue(h1 == h2, h2.ToString());
	  }
	}
}
