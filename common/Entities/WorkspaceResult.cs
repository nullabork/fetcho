﻿using System;
using System.Collections.Generic;

namespace Fetcho.Common.Entities
{
    public class WorkspaceResult
    {
        public string Hash { get; set; }

        public string ReferrerUri { get; set; }

        public string Uri { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public List<string> Tags { get; set; }

        public DateTime Created { get; set; }

        public long Sequence { get; set; }
    }
}