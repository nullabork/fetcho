
using System;
using System.Threading.Tasks;
using Fetcho.Common;
using Fetcho.Common.entities;
using NUnit.Framework;

namespace common
{
  [TestFixture]
  public class DatabaseTest
  {
    [Test]
    public void TestMethod()
    {
      var uri = new Uri("https://www.site1.com");
      var site = new Site() {HostName = uri.Host};

      Assert.IsTrue(site.Hash.Values.Length > 0, "No hash");
      Assert.IsTrue(site.HostName == "www.site1.com");
      Assert.IsTrue(site.IsBlocked == false);
      Assert.IsTrue(site.LastRobotsFetched == null);
      
      using ( var db = new Database("Server=127.0.0.1;Port=5432;User Id=getlinks;Password=getlinks;Database=fetcho;Enlist=false" ))
      {
        db.SaveSite(site).GetAwaiter().GetResult();
        
        
        site = db.GetSite(uri).GetAwaiter().GetResult();
        Assert.IsTrue(site != null, "Site was null");
      }
    }
    
    [Test]
    public async Task RobotsTest()
    {
      var r = await RobotsFile.GetFile(new Uri("https://www.wikipedia.org/"));
    }
  }
}
