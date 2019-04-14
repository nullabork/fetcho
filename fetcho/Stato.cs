using Fetcho.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Fetcho
{
    public class Stato : IDisposable
    {
        private StreamWriter writer;
        private Fetcho fetcho;
        private Queueo queueo;
        private ReadLinko reado;

        private List<StatInfo> Stats = new List<StatInfo>();
        private int completed = 0;

        public bool Running { get; set; }

        public Stato(string filepath, Fetcho fetcho, Queueo queueo, ReadLinko reado)
        {
            Running = true;
            this.fetcho = fetcho;
            this.queueo = queueo;
            this.reado = reado;
            writer = new StreamWriter(new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read));

            // fetcho stats
            Stats.Add(new StatInfo() { Header = "Chk #", Format = "{0,5}", Calculate = () => fetcho.ActiveChunkCount });
            Stats.Add(new StatInfo() { Header = "IP w#", Format = "{0,5}", Calculate = () => fetcho.WaitingFromIPCongestion });
            Stats.Add(new StatInfo() { Header = "Fe t#", Format = "{0,5}", Calculate = () => fetcho.WaitingForFetchTimeout });
            Stats.Add(new StatInfo() { Header = "Act #", Format = "{0,5}", Calculate = () => ResourceFetcher.ActiveFetches });
            Stats.Add(new StatInfo() { Header = "Wr w#", Format = "{0,5}", Calculate = () => ResourceFetcher.WaitingToWrite });
            Stats.Add(new StatInfo() { Header = " Exception #", Format = "{0,12}", Calculate = () => ResourceFetcher.FetchExceptions });
            Stats.Add(new StatInfo() { Header = " Completed #", Format = "{0,12}", Calculate = () => fetcho.CompletedFetches });
            Stats.Add(new StatInfo() { Header = "Uptime          ", Format = "{0,16}", Calculate = () => fetcho.Uptime });
            Stats.Add(new StatInfo()
            {
                Header = "pg/print",
                Format = "{0,8}",
                Calculate = () => {
                    var diff = fetcho.CompletedFetches - completed;
                    completed = fetcho.CompletedFetches;
                    return diff;
                }
            });
            Stats.Add(new StatInfo() { Header = "tot pg/m", Format = "{0,8}", Calculate = () => fetcho.TotalPagesPerMinute });

            // queue-o stats
            Stats.Add(new StatInfo() { Header = "Q In#", Format = "{0,5}", Calculate = () => queueo.InboxCount });
            Stats.Add(new StatInfo() { Header = "Duplicates #", Format = "{0,12}", Calculate = () => queueo.DuplicatesRejected });
            Stats.Add(new StatInfo() { Header = "Qp t#", Format = "{0,5}", Calculate = () => queueo.ActivePreQueueTasks });
            Stats.Add(new StatInfo() { Header = "Qq i#", Format = "{0,5}", Calculate = () => queueo.BufferCount });
            Stats.Add(new StatInfo() { Header = "DB w#", Format = "{0,5}", Calculate = () => DatabasePool.WaitingForDatabase });
            Stats.Add(new StatInfo() { Header = "Qv t#", Format = "{0,5}", Calculate = () => queueo.ActiveValidationTasks });
            Stats.Add(new StatInfo() { Header = "Qo i#", Format = "{0,5}", Calculate = () => queueo.OutboxCount });
            Stats.Add(new StatInfo() { Header = "    Accepted", Format = "{0,12}", Calculate = () => queueo.LinksAccepted });
            Stats.Add(new StatInfo() { Header = "    Rejected", Format = "{0,12}", Calculate = () => queueo.LinksRejected });

            Stats.Add(new StatInfo() { Header = "DSIdx", Format = "{0,5}", Calculate = () => reado.CurrentDataSourceIndex });
            Stats.Add(new StatInfo() { Header = "PktIx", Format = "{0,5}", Calculate = () => reado.CurrentPacketIndex });
            Stats.Add(new StatInfo() { Header = "    Rsc Proc", Format = "{0,12}", Calculate = () => reado.ResourcesProcessed });
            Stats.Add(new StatInfo() { Header = "   Extracted", Format = "{0,12}", Calculate = () => reado.LinksExtracted });
            Stats.Add(new StatInfo() { Header = "ROut", Format = "{0,4}", Calculate = () => reado.OutboxCount });

            writer.WriteLine(GetStatHeader());
        }

        public string GetStatHeader()
        {
            var sb = new StringBuilder();
            foreach (var stat in Stats)
            {
                sb.Append(stat.Header);
                sb.Append(' ');
            }
            return sb.ToString();
        }

        public string GetStatsString()
        {
            var sb = new StringBuilder();

            foreach (var stat in Stats)
            {
                sb.AppendFormat(stat.Format, stat.Calculate());
                sb.Append(' ');
            }
            sb.Append(DateTime.UtcNow.ToString("d/M/yyyy HH:mm"));

            return sb.ToString();

/*            return string.Format(
                "{0, 5}, {1, 5}, {2, 5}, {3, 5}, {4, 5}, {5, 12}, {6, 12},  {7, 12}, {8, 5}, " +
                "{9, 4}, {10, 12}, {11, 6}, {12, 6}, {13, 6}, {14, 4}, {15, 12}, {16, 12}, " +
                "{17, 6}, {18, 3}, {19, 6}, {20, 12}, {21, 4}, {22:d/M/yyyy HH:mm}"
                ,
                            fetcho.ActiveChunkCount,
                            fetcho.WaitingFromIPCongestion,
                            fetcho.WaitingForFetchTimeout,
                            ResourceFetcher.ActiveFetches,
                            ResourceFetcher.WaitingToWrite,
                            ResourceFetcher.FetchExceptions,
                            fetcho.CompletedFetches,
                            fetcho.Uptime,
                            fetcho.TotalPagesPerMinute,

                            queueo.InboxCount,
                            queueo.DuplicatesRejected,
                            queueo.BufferCount,
                            DatabasePool.WaitingForDatabase,
                            queueo.ActiveValidationTasks,
                            queueo.OutboxCount,
                            queueo.LinksAccepted,
                            queueo.LinksRejected,

                            reado.CurrentDataSourceIndex,
                            reado.CurrentPacketIndex,
                            reado.ResourcesProcessed,
                            reado.LinksExtracted,
                            reado.OutboxCount,
                            DateTime.UtcNow
                            );*/
        }

        public async Task Process()
        {
            Utility.LogInfo("Stato commencing Process()");

            while (Running)
            {
                try
                {
                    writer.WriteLine(GetStatsString());
                    writer.Flush();
                    await Task.Delay(FetchoConfiguration.Current.HowOftenToReportStatusInMilliseconds).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Utility.LogException(ex);
                }

            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    writer?.Dispose();
                    writer = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Stato() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        private class StatInfo
        {
            public string Header { get; set; }
            public string Format { get; set; }
            public Func<object> Calculate { get; set; }
        }
    }
}