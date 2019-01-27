
using System;
using System.Configuration;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Fetcho.Common.entities;
using Npgsql;
using log4net;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Data.Common;

namespace Fetcho.Common
{
    /// <summary>
    /// Connect to postgres
    /// </summary>
    public class Database : IDisposable
    {
        public const int ConnectionPoolWaitTimeInMilliseconds = 120000;

        static readonly BinaryFormatter formatter = new BinaryFormatter();
        public const int DefaultDatabasePort = 5432;
        public const int MaxConcurrentConnections = 10;
        static readonly ILog log = LogManager.GetLogger(typeof(Database));

        static readonly SemaphoreSlim connPool = new SemaphoreSlim(MaxConcurrentConnections);

        NpgsqlConnection conn;
        NpgsqlConnectionStringBuilder connstr;

        public string Server { get; set; }
        public int Port { get; set; }

        public Database(string server, int port)
        {
            Server = server;
            Port = port;
        }

        public Database(string connString)
        {
            connstr = new NpgsqlConnectionStringBuilder(connString);
            if (!String.IsNullOrWhiteSpace(connstr.Host))
                Server = connstr.Host;
            if (connstr.Port > 0)
                Port = connstr.Port;
        }

        public Database() : this(ConfigurationManager.ConnectionStrings["db"]?.ToString())
        {
        }

        /// <summary>
        /// open a connection to the DB
        /// </summary>
        /// <returns></returns>
        async Task Open(CancellationToken cancellationToken)
        {
            try
            {
                if (conn != null && conn.State == ConnectionState.Open) return;

                if (Port > 0)
                    connstr.Port = Port;
                if (!String.IsNullOrWhiteSpace(Server))
                    connstr.Host = Server;
                conn = new NpgsqlConnection(connstr.ToString());

                await WaitIfTooManyActiveConnections(cancellationToken);
                await conn.OpenAsync(cancellationToken);
                Server = conn.Host;
                Port = conn.Port;
            }
            catch (Exception ex)
            {
                log.Error("Open():  ", ex);
            }
        }

        /// <summary>
        /// Will block until a DB connection is available
        /// </summary>
        /// <returns></returns>
        async Task WaitIfTooManyActiveConnections(CancellationToken cancellationToken)
        {
            while (!await connPool.WaitAsync(ConnectionPoolWaitTimeInMilliseconds, cancellationToken))
                log.Info("Waiting for a database connection");
        }

        /// <summary>
        /// Setup a base NpgsqlCommand
        /// </summary>
        /// <param name="commandtext"></param>
        /// <returns></returns>
        async Task<NpgsqlCommand> SetupCommand(string commandtext, CancellationToken cancellationToken)
        {
            try
            {
                await Open(cancellationToken);

                NpgsqlCommand cmd = new NpgsqlCommand(commandtext)
                {
                    Connection = conn,
                    CommandTimeout = 600000
                };

                return cmd;
            }
            catch (Exception ex)
            {
                log.Error("SetupCommand():  ", ex);
                return null;
            }
        }

        public async Task<Site> GetSite(Uri anyUri, CancellationToken cancellationToken)
        {
            Site site = null;

            try
            {
                var hash = MD5Hash.Compute(anyUri.Host);

                NpgsqlCommand cmd = await SetupCommand("select hostname_hash, hostname, " +
                                                 "is_blocked, last_robots_fetched, robots_file " +
                                                 "from \"Site\" " +
                                                 "where hostname_hash = :hostname_hash", 
                                                 cancellationToken);

                cmd.Parameters.AddWithValue("hostname_hash", hash.Values);


                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    if (reader.Read())
                    {
                        site = new Site
                        {
                            HostName = reader.GetString(1),
                            IsBlocked = reader.GetBoolean(2),
                            LastRobotsFetched = reader.GetFieldValue<DateTime?>(3),
                            RobotsFile = DeserializeField<RobotsFile>(reader, 4)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("GetSite():  ", ex);
            }

            return site;

        }

        public async Task BlockSite(string hostName, CancellationToken cancellationToken)
        {
            NpgsqlCommand cmd = await SetupCommand("update \"Site\" set is_blocked = true " +
                                             "where hostname_hash = :hostname_hash", cancellationToken);

            cmd.Parameters.AddWithValue("hostname_hash", MD5Hash.Compute(hostName).Values);

            var c = await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<int> SaveSite(Site site, CancellationToken cancellationToken)
        {
            try
            {
                const string insert = "insert into \"Site\" (hostname_hash, hostname, is_blocked, last_robots_fetched, robots_file) " +
                  "values (:hostname_hash, :hostname, :is_blocked, :last_robots_fetched, :robots_file)";

                const string update = "update \"Site\" " +
                  "set    hostname = :hostname, " +
                  "       is_blocked = :is_blocked, " +
                  "       last_robots_fetched = :last_robots_fetched, " +
                  "       robots_file = :robots_file " +
                  "where  hostname_hash = :hostname_hash";

                NpgsqlCommand cmd = await SetupCommand(update, cancellationToken);

                cmd.Parameters.AddWithValue("hostname_hash", site.Hash.Values);
                cmd.Parameters.AddWithValue("hostname", site.HostName);
                cmd.Parameters.AddWithValue("is_blocked", site.IsBlocked);

                SetBinaryParameter(cmd, "robots_file", site.RobotsFile);

                if (site.LastRobotsFetched.HasValue)
                    cmd.Parameters.Add(new NpgsqlParameter<DateTime>("last_robots_fetched", site.LastRobotsFetched.Value));
                else
                    cmd.Parameters.AddWithValue("last_robots_fetched", DBNull.Value);

                int count = await cmd.ExecuteNonQueryAsync(cancellationToken);

                if (count == 0)
                {
                    cmd.CommandText = insert;
                    count = await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                return count;
            }
            catch (Exception ex)
            {
                log.Error("SaveSite():  ", ex);
                return 0;
            }
        }

        public async Task SaveWebResource(Uri uri, DateTime nextFetch, CancellationToken cancellationToken)
        {
            string updateSql = "set client_encoding='UTF8'; update \"WebResource\" " +
              "set    next_fetch = :next_fetch " +
              "where  urihash = :urihash;";

            string insertSql = "set client_encoding='UTF8'; insert into \"WebResource\" ( urihash, next_fetch ) " +
              "values                      ( :urihash, :next_fetch );";

            NpgsqlCommand cmd = await SetupCommand(updateSql, cancellationToken);
            _saveWebResourceSetParams(cmd, uri, nextFetch);
            int count = await cmd.ExecuteNonQueryAsync(cancellationToken);

            if (count == 0) // no record to update
            {
                cmd = await SetupCommand(insertSql, cancellationToken);
                _saveWebResourceSetParams(cmd, uri, nextFetch);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        static void _saveWebResourceSetParams(NpgsqlCommand cmd, Uri uri, DateTime nextfetch)
        {
            cmd.Parameters.Add(new NpgsqlParameter<byte[]>("urihash", MD5Hash.Compute(uri).Values));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("next_fetch", nextfetch));
        }

        /// <summary>
        /// Returns true if the URI needs visiting
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<bool> NeedsVisiting(Uri uri, CancellationToken cancellationToken)
        {
            // the logic here looks backward, but it deals with the case where there's no records!
            NpgsqlCommand cmd = await SetupCommand("select count(urihash) from \"WebResource\" where urihash = :urihash and next_fetch > now();", cancellationToken);

            cmd.Parameters.Add(new NpgsqlParameter("urihash", MD5Hash.Compute(uri).Values));

            object o = await cmd.ExecuteScalarAsync(cancellationToken);

            return ((long)o) == 0;
        }

        void SetBinaryParameter(NpgsqlCommand cmd, string parameterName, object value)
        {
            if (value != null)
                using (var ms = new MemoryStream(1000))
                {
                    formatter.Serialize(ms, value);
                    cmd.Parameters.AddWithValue(parameterName, ms.GetBuffer());
                }
            else
                cmd.Parameters.AddWithValue(parameterName, DBNull.Value);

        }

        /// <summary>
        /// Where an object is stored in the DB - deserialize
        /// </summary>
        /// <typeparam name="T">The class type to deserialize</typeparam>
        /// <param name="dataReader"></param>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        T DeserializeField<T>(DbDataReader dataReader, int ordinal) where T : class
        {
            if (dataReader.IsDBNull(ordinal)) return null;

            byte[] buffer = (byte[])dataReader.GetValue(ordinal);

            using (var ms = new MemoryStream(buffer))
            {
                return formatter.Deserialize(ms) as T;
            }
        }

        protected virtual void Dispose(bool disposable)
        {
            if (conn == null) return;
            conn.Close();
            conn.Dispose();
            conn = null;
            connPool.Release();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
