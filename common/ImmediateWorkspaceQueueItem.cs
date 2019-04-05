﻿using System;
using System.IO;
using System.Threading.Tasks;
using Fetcho.Common;
using Fetcho.ContentReaders;

namespace Fetcho.Common
{
    /// <summary>
    /// A queue item that immediately pushes the data to a workspace when its complete
    /// </summary>
    public class ImmediateWorkspaceQueueItem : QueueItem
    {
        public Guid DestinationWorkspaceId { get; set; }

        public string[] Tags { get; set; }
    }
}
