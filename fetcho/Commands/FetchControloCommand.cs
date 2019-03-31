using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fetcho.Common;

namespace Fetcho.Commands
{
    public class FetchControloCommand : ControloCommand
    {
        public FetchingTask Current = null;

        public override string CommandName => "fetch";

        public override string ShortHelp => "fetch subreddit [from_date] [to_date]";

        public override void Execute(Controlo controlo, string[] args)
        {
            if (args.Length < 1)
            {
                controlo.ReportError("Usage: {0}", ShortHelp);
                return;
            }

            string subcommand = args[0];

            if (subcommand == "status")
            {
                if (Current == null)
                {
                    controlo.ReportError("Fetching not in progress");
                    return;
                }

                controlo.ReportInfo("Total: {0}, Complete: {1}", Current.Items.Count, Current.Complete);
            }
            else if (subcommand == "reddit")
            {
                if (Current != null)
                {
                    controlo.ReportError("Fetching progress");
                    return;
                }

                string subreddit = args[1];

                controlo.ReportInfo("Getting the URLs from r/{0}", subreddit);
                var submissions = RedditSubmissionFetcher.GetSubmissions(subreddit).GetAwaiter().GetResult();
                Current = new FetchingTask();

                foreach (var submission in submissions)
                {
                    var queueItem = MakeQueueItem(submission);
                    Current.Items.Add(queueItem);
                    QueueItem(controlo, queueItem).GetAwaiter().GetResult();
                }

                controlo.ReportInfo("Task created");
            }
            else
            {
                controlo.ReportError("Usage: {0}", ShortHelp);
            }
        }

        private void QueueItemStatusUpdate(object sender, QueueItemStatusUpdateEventArgs e)
        {
            if (Current == null) return;

            if ( e.Status == QueueItemStatus.Discarded || e.Status == QueueItemStatus.Fetched )
            {
                Current.Process(e.Item, e.Status);
            }
        }

        private QueueItem MakeQueueItem(RedditSubmission submission)
        {
            try
            {
                var item = new QueueItem()
                {
                    SourceUri = null,
                    TargetUri = new Uri(submission.Url),
                    StatusUpdate = QueueItemStatusUpdate,
                };

                return item;
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
                return null;
            }
        }

        private async Task QueueItem(Controlo controlo, QueueItem item)
            => await controlo.PrioritisationBufferIn.SendOrWaitAsync(new[] { item });
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
