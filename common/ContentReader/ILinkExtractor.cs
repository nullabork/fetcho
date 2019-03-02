using System;

namespace Fetcho.ContentReaders
{
    public interface ILinkExtractor : IDisposable
    {
        Uri CurrentSourceUri { get; set; }
        Uri NextUri();
    }


}
