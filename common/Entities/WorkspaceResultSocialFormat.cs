using System;
using System.Collections.Generic;

namespace Fetcho.Common.Entities
{
    /// <summary>
    /// Format for publishing links to social media formats
    /// </summary>
    public class WorkspaceResultSocialFormat
    {
        #region WorkspaceResult fields

        public string UriHash { get; set; }

        public string Uri { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public List<string> Tags { get; }

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }

        public long GlobalSequence { get; set; }

        public string DataHash { get; set; }

        #endregion

        #region open graph fields

        public string Author { get; set; }

        public string ImageUrl { get; set; }

        public string ResultType { get; set; }

        public string SiteName { get; set; }

        #endregion

        public WorkspaceResultSocialFormat()
        {
            Tags = new List<string>();
        }

        public static IEnumerable<WorkspaceResultSocialFormat> FromWorkspaceResults(IEnumerable<WorkspaceResult> results)
        {
            var l = new List<WorkspaceResultSocialFormat>();

            foreach( var result in results )
            {
                l.Add(new WorkspaceResultSocialFormat
                {
                    Title = result.Title,
                    Description = result.Description,
                    UriHash = result.UriHash,
                    Uri = result.Uri,
                    Created = result.Created,
                    Updated = result.Updated,
                    GlobalSequence = result.GlobalSequence,
                    DataHash = result.DataHash
                });

                l[l.Count - 1].Tags.AddRange(result.Tags);
            }

            return l;
        }

    }
}