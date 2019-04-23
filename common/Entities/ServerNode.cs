using System;

namespace Fetcho.Common.Entities
{
    /// <summary>
    /// Represents the details for a specific ServerNode in the group crawling and searching the internet
    /// </summary>
    public class ServerNode
    {
        public Guid ServerId { get; set; }

        public string Name { get; set; }

        public HashRange UriHashRange { get; set; }

        public bool IsApproved { get; set; }

        public DateTime Created { get; set; }

        public ServerNode()
        {
            ServerId = Guid.NewGuid();
            Name = Environment.MachineName;
            Created = DateTime.Now;
            IsApproved = false;
            UriHashRange = HashRange.Largest;
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}", Name, UriHashRange);
        }

    }
}
