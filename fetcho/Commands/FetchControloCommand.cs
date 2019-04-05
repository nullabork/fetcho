using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Fetcho.Common;

namespace Fetcho.Commands
{
    public class FetchControloCommand : ControloCommand
    {
        public FetchingTask Current = null;

        public override string CommandName => "fetch";

        public override string ShortHelp => "fetch reddit [subreddit] [dest workspace id]";

        public override void Execute(string[] args)
        {
            if (args.Length < 1)
            {
                Controlo.ReportError("Usage: {0}", ShortHelp);
                return;
            }

            string subcommand = args[0];

            if (subcommand == "status")
            {
                if (Current == null)
                {
                    Controlo.ReportError("Fetching not in progress");
                    return;
                }

                Controlo.ReportInfo("Total: {0}, Complete: {1}", Current.Items.Count, Current.Complete);
            }
            else if (subcommand == "reddit")
            {
                if (Current != null)
                {
                    Controlo.ReportError("Fetching in progress");
                    return;
                }

                if ( args.Length < 3 )
                {
                    Controlo.ReportError("Usage: {0}", ShortHelp);
                    return;
                }

                if ( !Guid.TryParse(args[2], out Guid destinationWorkspaceId))
                {
                    Controlo.ReportError("No destination workspace id specified or invalid GUID: {0}", args[2]);
                    return;
                }

                string subreddit = args[1];

                Controlo.ReportInfo("Getting the URLs from r/{0}", subreddit);
                var submissions = RedditSubmissionFetcher.GetSubmissions(subreddit).GetAwaiter().GetResult();
                Controlo.ReportInfo("{0} submissions extracted from r/{1}", submissions.Count(), subreddit);
                Current = new FetchingTask();
                SetupWorkspaceDataWriter().GetAwaiter().GetResult();

                foreach (var submission in submissions)
                {
                    var queueItem = MakeQueueItem(submission, destinationWorkspaceId);
                    Current.Items.Add(queueItem);
                    SendItemForQueuing(queueItem).GetAwaiter().GetResult();
                }

                Controlo.ReportInfo("Task created");
            }
            else
            {
                Controlo.ReportError("Usage: {0}", ShortHelp);
            }
        }

        private async Task SetupWorkspaceDataWriter()
        {
            var writer = Controlo.DataWriterPool.Receive();
            if (writer is WebDataPacketWriter webDataPacketWriter)
                writer = new WorkspaceResourcePacketWriter(webDataPacketWriter);
            await Controlo.DataWriterPool.SendOrWaitAsync(writer);
        }

        private void QueueItemStatusUpdate(object sender, QueueItemStatusUpdateEventArgs e)
        {
            if (Current == null) return;

            if ( e.Status == QueueItemStatus.Discarded || e.Status == QueueItemStatus.Fetched )
            {
                Current.Process(e.Item, e.Status);
            }
        }

        private QueueItem MakeQueueItem(RedditSubmission submission, Guid destinationWorkspaceId)
        {
            try
            {
                var item = new ImmediateWorkspaceQueueItem()
                {
                    SourceUri = null,
                    TargetUri = new Uri(submission.Url),
                    StatusUpdate = QueueItemStatusUpdate,
                    DestinationWorkspaceId = destinationWorkspaceId,
                    ReadTimeoutInMilliseconds = 120000, // big
                    Tags = Utility.MakeTags(submission.LinkFlairText.Split(',')).ToArray(),
                    CanBeDiscarded = false
                };

                return item;
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
                return null;
            }
        }

        private async Task SendItemForQueuing(QueueItem item)
            => await Controlo.PrioritisationBufferIn.SendOrWaitAsync(new[] { item });
    }

    public class FetchingTask
    {
        public HashSet<QueueItem> Items { get; set; }

        public int Complete { get; set; }

        public bool IsComplete { get => Complete == Items.Count; }

        public FetchingTask()
        {
            Items = new HashSet<QueueItem>();
        }

        public void Process(QueueItem item, QueueItemStatus status)
        {
            if (!Items.Contains(item)) return;

            if (status == QueueItemStatus.Discarded || status == QueueItemStatus.Fetched)
                Complete++;
        }
    }
}
