
using System;

namespace Fetcho.Common.entites
{
  /// <summary>
  /// Description of WebResource.
  /// </summary>
  public class WebResource
  {
    public MD5Hash UriHash { get; set; }
    
    public DateTime LastFetched { get; set; }
    
    public WebResource()
    {
    }
  }
}
