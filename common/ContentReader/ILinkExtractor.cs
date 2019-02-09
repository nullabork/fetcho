using System;

namespace Fetcho.ContentReaders
{
    public interface ILinkExtractor
    {
        Uri CurrentSourceUri { get; set; }
        Uri NextUri();
    }


}
