using Fetcho.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace getlinks_core.tests
{
    [TestClass]
	public class Md5HashTest
	{
	  [TestMethod]
	  public void EqualsTest()
	  {
	    Assert.IsTrue(MD5Hash.MinValue == MD5Hash.MinValue );
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
