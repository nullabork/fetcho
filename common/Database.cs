
using System;
using System.Configuration;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Fetcho.Common.Entities;
using Npgsql;
using log4net;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;

namespace Fetcho.Common
{
    /// <summary>
    /// Connect to postgres, run commands
    /// </summary>
    public class Database : IDisposable
    {
        public const int ConnectionPoolWaitTimeInMilliseconds = 120000;
        public const int DefaultDatabasePort = 5432;
        public const int MaxConcurrentConnections = 50;
        public const int WaitTimeVarianceInMilliseconds = 250;

        static readonly ILog log = LogManager.GetLogger(typeof(Database));
        static readonly Random random = new Random(DateTime.UtcNow.Millisecond);
        static readonly SemaphoreSlim connPool = new SemaphoreSlim(MaxConcurrentConnections);

        private NpgsqlConnection conn;
        private NpgsqlConnectionStringBuilder connstr;
        private bool IsOpen { get => (conn != null && conn.State == ConnectionState.Open); }

        public string Server { get; set; }
        public int Port { get; set; }

        public Database(string server, int port)
        {
            Server = server;
            Port = port;
        }

        public Database(string connectionString)
        {
            if (String.IsNullOrWhiteSpace(connectionString))
                throw new FetchoException("db connection string is not configured");
            connstr = new NpgsqlConnectionStringBuilder(connectionString);
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
        public async Task Open()
        {
            try
            {
                if (IsOpen) return;

                if (Port > 0)
                    connstr.Port = Port;
                if (!String.IsNullOrWhiteSpace(Server))
                    connstr.Host = Server;
                conn = new NpgsqlConnection(connstr.ToString());

                await WaitIfTooManyActiveConnections().ConfigureAwait(false);
                await conn.OpenAsync().ConfigureAwait(false);
                Server = conn.Host;
                Port = conn.Port;
            }
            catch (Exception ex)
            {
                log.Error("Open(): {0}", ex);
            }
        }

        /// <summary>
        /// Will block until a DB connection is available
        /// </summary>
        /// <returns></returns>
        async Task WaitIfTooManyActiveConnections()
        {
            DateTime start = DateTime.UtcNow;
            while (!await connPool.WaitAsync(GetWaitTime()).ConfigureAwait(false))
                log.InfoFormat("Waiting for a database connection for {0}ms",
                    (DateTime.UtcNow - start).TotalMilliseconds);
        }

        /// <summary>
        /// Setup a base NpgsqlCommand
        /// </summary>
        /// <param name="commandtext"></param>
        /// <returns></returns>
        async Task<NpgsqlCommand> SetupCommand(string commandtext)
        {
            try
            {
                if (!IsOpen) await Open().ConfigureAwait(false);

                NpgsqlCommand cmd = new NpgsqlCommand(commandtext)
                {
                    Connection = conn,
                    CommandTimeout = 600000
                };

                return cmd;
            }
            catch (Exception ex)
            {
                log.Error("SetupCommand(): {0}", ex);
                return null;
            }
        }

        public async Task<Site> GetSite(Uri anyUri)
        {
            Site site = null;

            try
            {
                var hash = MD5Hash.Compute(anyUri.Host);

                NpgsqlCommand cmd = await SetupCommand("select hostname_hash, hostname, " +
                                                 "is_blocked, last_robots_fetched, robots_file, " +
                                                 "uses_compression, uses_encryption " +
                                                 "from \"Site\" " +
                                                 "where hostname_hash = :hostname_hash"
                                                 );

                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("hostname_hash", hash.Values));
                cmd.Prepare();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (reader.Read())
                    {
                        site = new Site
                        {
                            HostName = reader.GetString(1),
                            IsBlocked = reader.GetBoolean(2),
                            LastRobotsFetched = reader.GetFieldValue<DateTime?>(3),
                            RobotsFile = reader.DeserializeField<RobotsFile>(4),
                            UsesCompression = reader.GetBoolean(5),
                            UsesEncryption = reader.GetBoolean(6)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("GetSite(): {0}", ex);
            }

            return site;

        }

        public async Task BlockSite(string hostName)
        {
            NpgsqlCommand cmd = await SetupCommand("update \"Site\" set is_blocked = true " +
                                             "where hostname_hash = :hostname_hash");

            cmd.Parameters.AddWithValue("hostname_hash", MD5Hash.Compute(hostName).Values);

            var c = await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> SaveSite(Site site)
        {
            try
            {
                const string insert = "insert into \"Site\" (hostname_hash, hostname, is_blocked, last_robots_fetched, robots_file, uses_compression, uses_encryption) " +
                  "values (:hostname_hash, :hostname, :is_blocked, :last_robots_fetched, :robots_file, :uses_compression, :uses_encryption)";

                const string update = "update \"Site\" " +
                  "set    hostname = :hostname, " +
                  "       is_blocked = :is_blocked, " +
                  "       last_robots_fetched = :last_robots_fetched, " +
                  "       robots_file = :robots_file, " +
                  "       uses_compression = :uses_compression, " +
                  "       uses_encryption = :uses_encryption " +
                  "where  hostname_hash = :hostname_hash";

                NpgsqlCommand cmd = await SetupCommand(update);

                cmd.Parameters.AddWithValue("hostname_hash", site.Hash.Values);
                cmd.Parameters.AddWithValue("hostname", site.HostName);
                cmd.Parameters.AddWithValue("is_blocked", site.IsBlocked);
                cmd.Parameters.AddWithValue("uses_compression", site.UsesCompression);
                cmd.Parameters.AddWithValue("uses_encryption", site.UsesEncryption);
                cmd.SetBinaryParameter("robots_file", site.RobotsFile);

                if (site.LastRobotsFetched.HasValue)
                    cmd.Parameters.Add(new NpgsqlParameter<DateTime>("last_robots_fetched", site.LastRobotsFetched.Value));
                else
                    cmd.Parameters.AddWithValue("last_robots_fetched", DBNull.Value);
                cmd.Prepare();

                int count = await cmd.ExecuteNonQueryAsync();

                if (count == 0)
                {
                    cmd.CommandText = insert;
                    cmd.Prepare();
                    count = await cmd.ExecuteNonQueryAsync();
                }

                return count;
            }
            catch (Exception ex)
            {
                log.Error("SaveSite(): {0}", ex);
                return 0;
            }
        }

        public async Task SaveWebResource(Uri uri, DateTime nextFetch)
        {
            try
            {
                string updateSql = "set client_encoding='UTF8'; update \"WebResource\" " +
                  "set    next_fetch = :next_fetch " +
                  "where  urihash = :urihash;";

                string insertSql = "set client_encoding='UTF8'; insert into \"WebResource\" ( urihash, next_fetch ) " +
                  "values                      ( :urihash, :next_fetch );";

                NpgsqlCommand cmd = await SetupCommand(updateSql).ConfigureAwait(false);
                _saveWebResourceSetParams(cmd, uri, nextFetch);
                int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                if (count == 0) // no record to update
                {
                    cmd = await SetupCommand(insertSql).ConfigureAwait(false);
                    _saveWebResourceSetParams(cmd, uri, nextFetch);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                log.ErrorFormat("SaveWebResource(): {0}", ex);
            }
        }

        static void _saveWebResourceSetParams(NpgsqlCommand cmd, Uri uri, DateTime nextfetch)
        {
            cmd.Parameters.Add(new NpgsqlParameter<byte[]>("urihash", MD5Hash.Compute(uri).Values));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("next_fetch", nextfetch));
            cmd.Prepare();
        }

        public async Task<IEnumerable<MD5Hash>> NeedsVisiting(Uri uri)
            => await NeedsVisiting(new[] { MD5Hash.Compute(uri) });

        /// <summary>
        /// Returns true if the URI needs visiting
        /// </summary>
        /// <param name="uri"></param>
        /// <returns>The list of hashses that need visiting</returns>
        public async Task<IEnumerable<MD5Hash>> NeedsVisiting(IEnumerable<MD5Hash> hashes)
        {
            try
            {
                if (!hashes.Any()) return hashes;
                string str = hashes.Aggregate("", (s, hash) => string.Format("{0}decode('{1}', 'hex'),", s, hash));
                str = str.Substring(0, str.Length - 1);
                string sql = string.Format("select urihash " +
                                           "from   \"WebResource\" " +
                                           "where  urihash in({0}) " +
                                           "       and next_fetch > :next_fetch " +
                                           "limit  :maxcount;", str);

                // the logic here looks backward, but it deals with the case where there's no records!
                var cmd = await SetupCommand(sql);

                var l = new HashSet<MD5Hash>(hashes);

                cmd.Parameters.Add(new NpgsqlParameter<DateTime>("next_fetch", DateTime.UtcNow));
                cmd.Parameters.Add(new NpgsqlParameter<int>("maxcount", hashes.Count()));
                //cmd.Prepare();

                var n = new List<MD5Hash>();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var buffer = new Byte[MD5Hash.ExpectedByteLength];
                    while (reader.Read())
                    {
                        reader.GetBytes(0, 0, buffer, 0, MD5Hash.ExpectedByteLength);
                        l.Remove(new MD5Hash(buffer));
                    }
                }

                return l;
            }
            catch (Exception ex)
            {
                log.ErrorFormat("NeedsVisiting(): {0}", ex);
                return hashes;
            }
        }

        /// <summary>
        /// Determines if an account can access a specific workspace
        /// </summary>
        /// <param name="workspaceId"></param>
        /// <returns></returns>
        public async Task<bool> HasWorkspaceAccess(Guid workspaceId, string accountName)
        {
            string sql =
                "select count(*) " +
                "from   \"WorkspaceAccessKey\" " +
                "where  workspace_id = :workspace_id " +
                "       and account_name = :account_name;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
            cmd.Parameters.Add(new NpgsqlParameter<string>("account_name", accountName));
            return (long)(await cmd.ExecuteScalarAsync().ConfigureAwait(false)) > 0;
        }

        /// <summary>
        /// Get a workspace record by it's GUID
        /// </summary>
        /// <param name="workspaceId"></param>
        /// <returns></returns>
        public async Task<Workspace> GetWorkspace(Guid workspaceId)
        {
            Workspace workspace = null;

            string sql =
                "select workspace_id, name, description, is_active, query_text, result_count, created, is_wellknown, revision " +
                "from   \"Workspace\" " +
                "where  workspace_id = :workspace_id;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));

            // can't run this here until MARS is implemented on Npgsql
            // https://github.com/npgsql/npgsql/issues/462
            //var keys = GetWorkspaceAccessKeys(guid);

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                if (reader.Read())
                {
                    workspace = new Workspace
                    {
                        WorkspaceId = reader.GetGuid(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? String.Empty : reader.GetFieldValue<string>(2),
                        IsActive = reader.GetBoolean(3),
                        QueryText = reader.GetString(4),
                        ResultCount = reader.GetInt64(5),
                        Created = reader.GetDateTime(6),
                        IsWellknown = reader.GetBoolean(7),
                        Revision = reader.GetInt32(8)
                    };
                }
            }

            // see MARS comment
            //workspace.AccessKeys.AddRange(await keys);

            workspace?.AccessKeys.AddRange(await GetWorkspaceAccessKeys(workspaceId));

            return workspace;
        }

        /// <summary>
        /// Get a workspace record by it's GUID
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        public async Task<IEnumerable<Workspace>> GetWorkspaces()
        {
            Workspace workspace = null;

            string sql =
                "select workspace_id, name, description, is_active, query_text, result_count, created, is_wellknown, revision " +
                "from   \"Workspace\"; ";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

            var l = new List<Workspace>();

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (reader.Read())
                {
                    workspace = new Workspace
                    {
                        WorkspaceId = reader.GetGuid(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? String.Empty : reader.GetFieldValue<string>(2),
                        IsActive = reader.GetBoolean(3),
                        QueryText = reader.GetString(4),
                        ResultCount = reader.GetInt64(5),
                        Created = reader.GetDateTime(6),
                        IsWellknown = reader.GetBoolean(7),
                        Revision = reader.GetInt32(8)
                    };

                    l.Add(workspace);
                }
            }

            return l;
        }

        public async Task<Guid> GetWorkspaceIdByAccessKey(Guid accessKeyId)
        {
            Guid guid = Guid.Empty;

            string sql =
                "select workspace_id " +
                "from   \"WorkspaceAccessKey\" " +
                "where  workspace_access_key_id = :access_key_id;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("access_key_id", accessKeyId));

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                if (reader.Read())
                {
                    guid = reader.GetGuid(0);
                }
            }

            return guid;
        }

        /// <summary>
        /// Returns true if the account name is valid
        /// </summary>
        /// <param name="accountName"></param>
        /// <returns></returns>
        public async Task<bool> IsValidAccountName(string accountName)
        {
            var k = await GetAccount(accountName);
            return k != null && k.IsActive;
        }

        public async Task SaveWorkspace(Workspace workspace)
        {
            string updateSql = "set client_encoding='UTF8'; update \"Workspace\" " +
              "set    name = :name, " +
              "       description = :description, " +
              "       query_text = :query_text, " +
              "       is_active = :is_active, " +
              "       is_wellknown = :is_wellknown, " +
              "       revision = :revision " +
              "where  workspace_id = :workspace_id;";

            string insertSql = "set client_encoding='UTF8'; insert into \"Workspace\" ( workspace_id, name, description, query_text, is_active, created, is_wellknown, revision ) " +
              "values ( :workspace_id, :name, :description, :query_text, :is_active, :created, :is_wellknown, :revision );";

            NpgsqlCommand cmd = await SetupCommand(updateSql).ConfigureAwait(false);
            _saveWorkspaceSetParams(cmd, workspace);
            int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (count == 0) // no record to update
            {
                cmd = await SetupCommand(insertSql).ConfigureAwait(false);
                _saveWorkspaceSetParams(cmd, workspace);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await SaveAccessKeys(workspace);

        }

        /// <summary>
        /// Get a workspace record by it's GUID
        /// </summary>
        /// <param name="workspaceId"></param>
        /// <returns></returns>
        public async Task DeleteWorkspace(Guid workspaceId)
        {

            string sql =
                "delete  " +
                "from   \"Workspace\" " +
                "where  workspace_id = :workspace_id;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
            int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (count == 0)
                throw new FetchoException("No record was deleted");
        }

        private void _saveWorkspaceSetParams(NpgsqlCommand cmd, Workspace workspace)
        {
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspace.WorkspaceId.GetValueOrDefault()));
            cmd.Parameters.Add(new NpgsqlParameter<string>("name", workspace.Name));
            cmd.Parameters.Add(new NpgsqlParameter<string>("description", workspace.Description));
            cmd.Parameters.Add(new NpgsqlParameter<string>("query_text", workspace.QueryText));
            cmd.Parameters.Add(new NpgsqlParameter<bool>("is_active", workspace.IsActive));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("created", workspace.Created));
            cmd.Parameters.Add(new NpgsqlParameter<bool>("is_wellknown", workspace.IsWellknown));
            cmd.Parameters.Add(new NpgsqlParameter<int>("revision", workspace.Revision));
            cmd.Prepare();
        }

        public async Task SaveAccount(Account account)
        {
            string updateSql = "set client_encoding='UTF8'; update \"Account\" " +
              "set    is_active = :is_active " +
              "where  name = :name;";

            string insertSql = "set client_encoding='UTF8'; insert into \"Account\" ( name, is_active, created) " +
              "values ( :name, :is_active, :created);";

            NpgsqlCommand cmd = await SetupCommand(updateSql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<string>("name", account.Name));
            cmd.Parameters.Add(new NpgsqlParameter<bool>("is_active", account.IsActive));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("created", account.Created));
            int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (count == 0) // no record to update
            {
                cmd.CommandText = insertSql;
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<Account> GetAccount(string accountName)
        {
            string sql = "select name, is_active, created from \"Account\" where name = :name;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<string>("name", accountName));

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (reader.Read())
                {
                    var ak = new Account()
                    {
                        Name = reader.GetString(0),
                        IsActive = reader.GetBoolean(1),
                        Created = reader.GetDateTime(2)
                    };

                    return ak;
                }
            }

            return null;
        }

        public async Task DeleteAccount(string accountName)
        {
            string sql = "delete from \"Account\" where name = :name;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<string>("name", accountName));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task SetAccountProperty(string accountName, string name, string value)
        {
            string updateSql =
                "update \"AccountProperty\" " +
                "set    value = :value " +
                "where  account_name = :account_name and name = :name;";

            string insertSql =
                "insert into \"AccountProperty\" (name, value, account_name, created) values(:name, :value, :account_name, :created);";

            NpgsqlCommand cmd = await SetupCommand(updateSql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<string>("name", name));
            cmd.Parameters.Add(new NpgsqlParameter<string>("value", value));
            cmd.Parameters.Add(new NpgsqlParameter<string>("account_name", accountName));
            int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (count == 0) // no record to update
            {
                cmd.CommandText = insertSql;
                cmd.Parameters.Add(new NpgsqlParameter<DateTime>("created", DateTime.UtcNow));
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<AccountProperty> GetAccountProperty(string accountName, string name)
        {
            string sql = "select name, value from \"AccountProperty\" where account_name = :account_name and name = :name;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<string>("name", name));
            cmd.Parameters.Add(new NpgsqlParameter<string>("account_name", accountName));

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                if (reader.Read())
                {
                    return new AccountProperty()
                    {
                        Key = reader.GetString(0),
                        Value = reader.IsDBNull(1) ? String.Empty : reader.GetString(1)
                    };
                }
            }

            return null;
        }

        public async Task<IEnumerable<AccountProperty>> GetAccountProperties(string accountName)
        {
            string sql = "select name, value from \"AccountProperty\" where account_name = :account_name;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<string>("account_name", accountName));

            var l = new List<AccountProperty>();

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (reader.Read())
                {
                    l.Add(new AccountProperty()
                    {
                        Key = reader.GetString(0),
                        Value = reader.IsDBNull(1) ? String.Empty : reader.GetString(1)
                    });
                }
            }

            return l;
        }

        public async Task DeleteWorkspaceKey(Guid workspaceAccessKeyId)
        {
            string sql = "delete from \"WorkspaceAccessKey\" where workspace_access_key_id = :workspace_access_key_id;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_access_key_id", workspaceAccessKeyId));
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Delete workspace results by the workspace id
        /// </summary>
        public async Task<int> DeleteWorkspaceResultsByWorkspaceId(Guid workspaceId, IEnumerable<WorkspaceResult> results)
        {

            string sql =
                "delete  " +
                "from   \"WorkspaceResult\" " +
                "where  workspace_id = :workspace_id " +
                "       and urihash = :urihash;";

            int total = 0;

            foreach (var result in results)
            {
                NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
                cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("urihash", new MD5Hash(result.UriHash).Values));
                cmd.Prepare();

                int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                total += count;
            }

            if (total == 0)
                throw new FetchoException("No record was deleted");

            return total;
        }

        /// <summary>
        /// Delete workspace results by the workspace id
        /// </summary>
        public async Task<int> DeleteWorkspaceResultsByWorkspaceId(Guid workspaceId, IEnumerable<MD5Hash> urihashes)
        {

            string sql =
                "delete  " +
                "from   \"WorkspaceResult\" " +
                "where  workspace_id = :workspace_id " +
                "       and urihash = :urihash;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

            int total = 0;

            foreach (var hash in urihashes)
            {
                cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("urihash", hash.Values));
                cmd.Prepare();

                int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                total += count;
            }

            if (total == 0)
                throw new FetchoException("No records were deleted");

            return total;
        }

        public async Task<int> DeleteAllWorkspaceResultsByWorkspaceId(Guid workspaceId)
        {
            string sql =
                "delete  " +
                "from   \"WorkspaceResult\" " +
                "where  workspace_id = :workspace_id;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
            cmd.Prepare();

            int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (count == 0)
                throw new FetchoException("No records were deleted");

            return count;
        }

        public async Task<int> DeleteWorkspaceResultByWorkspaceId(Guid workspaceId, MD5Hash urihash)
            => await DeleteWorkspaceResultsByWorkspaceId(workspaceId, new[] { urihash });

        public async Task<int> MoveAllWorkspaceResultsByWorkspaceId(Guid sourceWorkspaceId, Guid destinationWorkspaceId)
        {
            string sql =
                "update \"WorkspaceResult\" " +
                "set    workspace_id = :destination_workspace_id " +
                "where  workspace_id = :source_workspace_id;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

            cmd.Parameters.Add(new NpgsqlParameter<Guid>("source_workspace_id", sourceWorkspaceId));
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("destination_workspace_id", destinationWorkspaceId));
            cmd.Prepare();

            int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (count == 0)
                throw new FetchoException("No records were moved");

            return count;
        }

        public async Task<int> MoveWorkspaceResultsByWorkspaceId(Guid sourceWorkspaceId, Guid destinationWorkspaceId, IEnumerable<MD5Hash> hashes)
        {
            string sql =
                "update \"WorkspaceResult\" " +
                "set    workspace_id = :destination_workspace_id " +
                "where  workspace_id = :source_workspace_id and urihash = :urihash;";

            int total = 0;

            foreach (var hash in hashes)
            {
                NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

                cmd.Parameters.Add(new NpgsqlParameter<Guid>("source_workspace_id", sourceWorkspaceId));
                cmd.Parameters.Add(new NpgsqlParameter<Guid>("destination_workspace_id", destinationWorkspaceId));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("urihash", hash.Values));
                cmd.Prepare();

                int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                total += count;
            }

            if (total == 0)
                throw new FetchoException("No records were moved");

            return total;
        }

        public async Task<int> MoveWorkspaceResultsByWorkspaceId(Guid sourceWorkspaceId, Guid destinationWorkspaceId, IEnumerable<WorkspaceResult> results)
            => await MoveWorkspaceResultsByWorkspaceId(sourceWorkspaceId, destinationWorkspaceId, results.Select(x => new MD5Hash(x.UriHash)));

        public async Task<int> MoveWorkspaceResultByWorkspaceId(Guid sourceWorkspaceId, Guid destinationWorkspaceId, MD5Hash urihash)
            => await MoveWorkspaceResultsByWorkspaceId(sourceWorkspaceId, destinationWorkspaceId, new[] { urihash });

        public async Task<int> CopyAllWorkspaceResultsByWorkspaceId(Guid sourceWorkspaceId, Guid destinationWorkspaceId)
        {
            string sql =
                "insert int \"WorkspaceResult\"( urihash, uri, referer, title, description, created, workspace_id, page_size, tags, datahash, updated, features, source_server_id) " +
                "select urihash, uri, referer, title, description, created, :destination_workspace_id, page_size, tags, datahash, updated, features, source_server_id " +
                "from   \"WorkspaceResult\" " +
                "where  workspace_id = :source_workspace_id;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

            cmd.Parameters.Add(new NpgsqlParameter<Guid>("source_workspace_id", sourceWorkspaceId));
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("destination_workspace_id", destinationWorkspaceId));
            cmd.Prepare();

            int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (count == 0)
                throw new FetchoException("No records were copied");

            return count;
        }

        public async Task<int> CopyWorkspaceResultsByWorkspaceId(Guid sourceWorkspaceId, Guid destinationWorkspaceId, IEnumerable<MD5Hash> hashes)
        {
            string sql =
                "insert int \"WorkspaceResult\"( urihash, uri, referer, title, description, created, workspace_id, page_size, tags, datahash, updated, features, source_server_id) " +
                "select urihash, uri, referer, title, description, created, :destination_workspace_id, page_size, tags, datahash, updated, features, source_server_id " +
                "from   \"WorkspaceResult\" " +
                "where  workspace_id = :source_workspace_id and urihash = :urihash;";

            int total = 0;

            foreach (var hash in hashes)
            {
                NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

                cmd.Parameters.Add(new NpgsqlParameter<Guid>("source_workspace_id", sourceWorkspaceId));
                cmd.Parameters.Add(new NpgsqlParameter<Guid>("destination_workspace_id", destinationWorkspaceId));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("urihash", hash.Values));
                cmd.Prepare();

                int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                total += count;
            }

            if (total == 0)
                throw new FetchoException("No records were copied");

            return total;
        }

        public async Task<int> CopyWorkspaceResultsByWorkspaceId(Guid sourceWorkspaceId, Guid destinationWorkspaceId, IEnumerable<WorkspaceResult> results)
            => await CopyWorkspaceResultsByWorkspaceId(sourceWorkspaceId, destinationWorkspaceId, results.Select(x => new MD5Hash(x.UriHash)));

        public async Task<int> CopyWorkspaceResultByWorkspaceId(Guid sourceWorkspaceId, Guid destinationWorkspaceId, MD5Hash urihash)
            => await CopyWorkspaceResultsByWorkspaceId(sourceWorkspaceId, destinationWorkspaceId, new[] { urihash });

        public async Task<int> TagAllWorkspaceResultsByWorkspaceId(Guid workspaceId, string tag)
        {
            string sql =
                "update \"WorkspaceResult\" " +
                "set    tags = array_to_string(array(SELECT DISTINCT e FROM unnest(array_append(string_to_array(tags, ' '), :new_tag)) AS a(e)), ' ' ) " +
                "where  workspace_id = :workspace_id;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

            cmd.Parameters.Add(new NpgsqlParameter<string>("new_tag", tag));
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
            cmd.Prepare();

            int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (count == 0)
                throw new FetchoException("No records were tagged");

            return count;
        }

        public async Task<int> TagWorkspaceResultsByWorkspaceId(Guid workspaceId, IEnumerable<MD5Hash> hashes, string tag)
        {
            string sql =
                "update \"WorkspaceResult\" " +
                "set    tags = array_to_string(array(SELECT DISTINCT e FROM unnest(array_append(string_to_array(tags, ' '), :new_tag)) AS a(e)), ' ' ) " +
                "where  workspace_id = :workspace_id and urihash = :urihash;";

            int total = 0;

            foreach (var hash in hashes)
            {
                NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

                cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("urihash", hash.Values));
                cmd.Parameters.Add(new NpgsqlParameter<string>("new_tag", tag));
                cmd.Prepare();

                int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                total += count;
            }

            if (total == 0)
                throw new FetchoException("No records were tagged");

            return total;
        }

        public async Task<int> TagWorkspaceResultsByWorkspaceId(Guid workspaceId, IEnumerable<WorkspaceResult> results, string tag)
            => await TagWorkspaceResultsByWorkspaceId(workspaceId, results.Select(x => new MD5Hash(x.UriHash)), tag);

        public async Task<int> TagWorkspaceResultByWorkspaceId(Guid workspaceId, MD5Hash urihash, string tag)
            => await TagWorkspaceResultsByWorkspaceId(workspaceId, new[] { urihash }, tag);

        public async Task<int> UntagAllWorkspaceResultsByWorkspaceId(Guid workspaceId, string tag)
        {
            string sql =
                "update \"WorkspaceResult\" " +
                "set    tags = array_to_string(array_remove(string_to_array(tags, ' '), :tag), ' ') " +
                "where  workspace_id = :workspace_id;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

            cmd.Parameters.Add(new NpgsqlParameter<string>("tag", tag));
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
            cmd.Prepare();

            int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (count == 0)
                throw new FetchoException("No records were tags");

            return count;
        }

        public async Task<int> UntagWorkspaceResultsByWorkspaceId(Guid workspaceId, IEnumerable<MD5Hash> hashes, string tag)
        {
            string sql =
                "update \"WorkspaceResult\" " +
                "set    tags = array_to_string(array_remove(string_to_array(tags, ' '), :tag), ' ') " +
                "where  workspace_id = :workspace_id and urihash = :urihash;";

            int total = 0;

            foreach (var hash in hashes)
            {
                NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

                cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("urihash", hash.Values));
                cmd.Parameters.Add(new NpgsqlParameter<string>("tag", tag));
                cmd.Prepare();

                int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                total += count;
            }

            if (total == 0)
                throw new FetchoException("No records were tags");

            return total;
        }

        public async Task<int> UntagWorkspaceResultsByWorkspaceId(Guid workspaceId, IEnumerable<WorkspaceResult> results, string tag)
            => await UntagWorkspaceResultsByWorkspaceId(workspaceId, results.Select(x => new MD5Hash(x.UriHash)), tag);

        public async Task<int> UntagWorkspaceResultByWorkspaceId(Guid workspaceId, MD5Hash urihash, string tag)
            => await UntagWorkspaceResultsByWorkspaceId(workspaceId, new[] { urihash }, tag);


        public async Task<IEnumerable<AccessKey>> GetWorkspaceAccessKeys(Guid workspaceId)
        {
            var l = new List<AccessKey>();

            string sql =
                "select workspace_access_key_id, workspace_id, account_name, is_active, permissions, created, expiry, is_wellknown, name, revision " +
                "from   \"WorkspaceAccessKey\" " +
                "where  workspace_id = :workspace_id;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (reader.Read())
                {
                    l.Add(new AccessKey()
                    {
                        Id = reader.GetGuid(0),
                        Name = reader.GetString(8),
                        AccountName = reader.GetString(2),
                        Created = reader.GetDateTime(5),
                        Expiry = reader.GetDateTime(6),
                        IsActive = reader.GetBoolean(3),
                        IsWellknown = reader.GetBoolean(7),
                        Permissions = (WorkspaceAccessPermissions)reader.GetInt32(4),
                        Revision = reader.GetInt32(9)
                    });
                }
            }

            return l;
        }

        public async Task AddWorkspaceQueryStats(WorkspaceQueryStats stats)
        {
            string sql =
                "insert into \"WorkspaceQueryStats\" (workspace_id, max_cost, avg_cost, total_cost, eval_count, include_count, exclude_count, tag_count, created) " +
                "values (:workspace_id, :max_cost, :avg_cost, :total_cost, :eval_count, :include_count, :exclude_count, :tag_count, :created);";

            var cmd = await SetupCommand(sql).ConfigureAwait(false);

            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", stats.WorkspaceId));
            cmd.Parameters.Add(new NpgsqlParameter<long>("max_cost", stats.MaxCost));
            cmd.Parameters.Add(new NpgsqlParameter<long>("avg_cost", stats.AvgCost));
            cmd.Parameters.Add(new NpgsqlParameter<long>("total_cost", stats.TotalCost));
            cmd.Parameters.Add(new NpgsqlParameter<long>("eval_count", stats.NumberOfEvaluations));
            cmd.Parameters.Add(new NpgsqlParameter<long>("include_count", stats.NumberOfInclusions));
            cmd.Parameters.Add(new NpgsqlParameter<long>("exclude_count", stats.NumberOfExclusions));
            cmd.Parameters.Add(new NpgsqlParameter<long>("tag_count", stats.NumberOfTags));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("created", stats.Created));

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<WorkspaceQueryStats>> GetWorkspaceQueryStatsByAccount(
            string accountName, Guid workspaceId, int offset = 0, int limit = 1)
        {
            string sql =
                "select w.workspace_id, q.max_cost, q.avg_cost, q.total_cost, q.eval_count, q.include_count, q.exclude_count, q.tag_count, q.sequence, q.created " +
                "from   \"WorkspaceQueryStats\" q inner join \"WorkspaceAccessKey\" w on w.workspace_id = q.workspace_id " +
                "where  w.account_name = :account_name " +
                (workspaceId == Guid.Empty ? "" : "    and w.workspace_id = :workspace_id ") +
                "order  by sequence desc " +
                "limit  " + limit + " offset " + offset + ";";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<string>("account_name", accountName));
            if (workspaceId != Guid.Empty)
                cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
            cmd.Prepare();

            var l = new List<WorkspaceQueryStats>();
            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (reader.Read())
                {
                    l.Add(new WorkspaceQueryStats()
                    {
                        WorkspaceId = reader.GetGuid(0),
                        MaxCost = reader.GetInt64(1),
                        AvgCost = reader.GetInt64(2),
                        TotalCost = reader.GetInt64(3),
                        NumberOfEvaluations = reader.GetInt64(4),
                        NumberOfInclusions = reader.GetInt64(5),
                        NumberOfExclusions = reader.GetInt64(6),
                        NumberOfTags = reader.GetInt64(7),
                        Sequence = reader.GetInt64(8),
                        Created = reader.GetDateTime(9)
                    });
                }
            }

            return l;
        }

        public async Task<IEnumerable<AccessKey>> GetAccessKeys(string accountName = "", bool includeWorkspace = false)
        {
            var l = new List<AccessKey>();

            string sql =
                "select workspace_access_key_id, workspace_id, account_name, is_active, permissions, created, expiry, is_wellknown, name, revision " +
                "from   \"WorkspaceAccessKey\" ";

            if (!String.IsNullOrWhiteSpace(accountName))
                sql += "where  account_name = :account_name;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

            if (!String.IsNullOrWhiteSpace(accountName))
                cmd.Parameters.Add(new NpgsqlParameter<string>("account_name", accountName));

            Dictionary<Guid, Guid> wsak = new Dictionary<Guid, Guid>();

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (reader.Read())
                {
                    l.Add(new AccessKey()
                    {
                        Id = reader.GetGuid(0),
                        AccountName = reader.GetString(2),
                        Created = reader.GetDateTime(5),
                        Expiry = reader.GetDateTime(6),
                        IsActive = reader.GetBoolean(3),
                        IsWellknown = reader.GetBoolean(7),
                        Name = reader.GetString(8),
                        Permissions = (WorkspaceAccessPermissions)reader.GetInt32(4),
                        Revision = reader.GetInt32(9)
                    });

                    wsak.Add(reader.GetGuid(0), reader.GetGuid(1));
                }
            }

            if (includeWorkspace)
                foreach (var k in l)
                    k.Workspace = await GetWorkspace(wsak[k.Id]);

            return l;
        }

        public async Task<AccessKey> GetAccessKey(Guid accessKeyId)
        {
            AccessKey k = null;
            Guid workspaceId = Guid.Empty;

            string sql =
                "select workspace_access_key_id, workspace_id, account_name, is_active, permissions, created, expiry, is_wellknown, name, revision " +
                "from   \"WorkspaceAccessKey\" " +
                "where  workspace_access_key_id = :access_key_id;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("access_key_id", accessKeyId));

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                if (reader.Read())
                {
                    k = new AccessKey()
                    {
                        Id = reader.GetGuid(0),
                        AccountName = reader.GetString(2),
                        Created = reader.GetDateTime(5),
                        Expiry = reader.GetDateTime(6),
                        IsActive = reader.GetBoolean(3),
                        IsWellknown = reader.GetBoolean(7),
                        Name = reader.GetString(8),
                        Permissions = (WorkspaceAccessPermissions)reader.GetInt32(4),
                        Revision = reader.GetInt32(9)
                    };

                    workspaceId = reader.GetGuid(1);
                }
            }

            if (k != null)
                k.Workspace = await GetWorkspace(workspaceId);

            return k;
        }

        public async Task SaveAccessKeys(Workspace workspace)
        {
            foreach (var k in workspace.AccessKeys)
                await SaveAccessKey(k);
        }

        public async Task SaveAccessKey(AccessKey workspaceAccessKey)
        {
            string updateSql =
                "set client_encoding='UTF8'; update \"WorkspaceAccessKey\" " +
              "set    is_active = :is_active, " +
              "       permissions = :permissions, " +
              "       name = :name, " +
              "       created = :created, " +
              "       expiry = :expiry, " +
              "       is_wellknown = :is_wellknown, " +
              "       revision = :revision " +
              "where  workspace_access_key_id = :workspace_access_key_id;";

            string insertSql = "set client_encoding='UTF8'; insert into \"WorkspaceAccessKey\" ( name, workspace_access_key_id, workspace_id, account_name, is_active, permissions, created, expiry, is_wellknown, revision) " +
              "values ( :name, :workspace_access_key_id, :workspace_id, :account_name, :is_active, :permissions, :created, :expiry, :is_wellknown, :revision);";

            NpgsqlCommand cmd = await SetupCommand(updateSql).ConfigureAwait(false);
            _saveAccessKeysSetParams(cmd, workspaceAccessKey);
            int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (count == 0) // no record to update
            {
                cmd = await SetupCommand(insertSql).ConfigureAwait(false);
                _saveAccessKeysSetParams(cmd, workspaceAccessKey);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

        }

        private void _saveAccessKeysSetParams(NpgsqlCommand cmd, AccessKey wak)
        {
            cmd.Parameters.Add(new NpgsqlParameter<string>("name", wak.Name));
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_access_key_id", wak.Id));
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", wak.Workspace.WorkspaceId.GetValueOrDefault()));
            cmd.Parameters.Add(new NpgsqlParameter<string>("account_name", wak.AccountName));
            cmd.Parameters.Add(new NpgsqlParameter<int>("permissions", (int)wak.Permissions));
            cmd.Parameters.Add(new NpgsqlParameter<bool>("is_active", wak.IsActive));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("created", wak.Created));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("expiry", wak.Expiry));
            cmd.Parameters.Add(new NpgsqlParameter<bool>("is_wellknown", wak.IsWellknown));
            cmd.Parameters.Add(new NpgsqlParameter<int>("revision", wak.Revision));
            cmd.Prepare();
        }

        public async Task<IEnumerable<WorkspaceResult>> GetWorkspaceResults(Guid workspaceId, long offset = -1, int count = -1, string orderBy = "sequence ASC")
        {
            var l = new List<WorkspaceResult>();

            string sql =
            WorkspaceResultSelectLine +
            "where  workspace_id = :workspace_id " +
            "order  by " + orderBy + " ";
            if (offset > -1 && count > -1)
                sql += "limit  " + count + " offset " + offset + ";";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
            cmd.Prepare();

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (reader.Read())
                {
                    l.Add(GetWorkspaceResultFromReader(reader));
                }
            }

            return l;
        }

        public async Task<IEnumerable<WorkspaceResult>> GetRandomWorkspaceResultsByWorkspaceId(Guid workspaceId, int count)
        {
            var l = new List<WorkspaceResult>();

            string sql =
            WorkspaceResultSelectLine +
            "where  workspace_id = :workspace_id and" +
            "       random > :random " +
            "order  by random " +
            "limit  " + count + ";";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
            cmd.Parameters.Add(new NpgsqlParameter<double>("random", random.NextDouble()));
            cmd.Prepare();

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (reader.Read())
                {
                    l.Add(GetWorkspaceResultFromReader(reader));
                }
            }

            return l;
        }

        public async Task<WorkspaceResult> GetWorkspaceResultByUri(Uri uri)
            => await GetWorkspaceResultByHash(MD5Hash.Compute(uri.ToString()));

        public async Task<WorkspaceResult> GetWorkspaceResultByHash(MD5Hash hash)
        {
            string sql =
            WorkspaceResultSelectLine +
            "where  urihash = :urihash";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<byte[]>("urihash", hash.Values));
            cmd.Prepare();

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                if (reader.Read())
                {
                    return GetWorkspaceResultFromReader(reader);
                }
            }

            return null;
        }

        public const string WorkspaceResultSelectLine =
            "select urihash, uri, referer, title, description, created, page_size, sequence, tags, datahash, updated, features, source_server_id " +
            "from   \"WorkspaceResult\" ";

        private WorkspaceResult GetWorkspaceResultFromReader(DbDataReader reader)
        {
            var r = new WorkspaceResult()
            {
                UriHash = reader.GetMD5Hash(0).ToString(),
                Uri = reader.GetString(1),
                RefererUri = IsNull(reader, 2, ""),
                Title = IsNull(reader, 3, ""),
                Description = IsNull(reader, 4, ""),
                Created = reader.GetDateTime(5),
                Updated = IsNull(reader, 10, DateTime.UtcNow),
                PageSize = reader.GetInt64(6),
                GlobalSequence = reader.GetInt64(7),
                DataHash = reader.IsDBNull(9) ? "" : reader.GetMD5Hash(9).ToString(),
                Features = reader.IsDBNull(11) ? new string[0] : (string[])reader.GetValue(11),
                SourceServerId = reader.GetGuid(12)
            };

            if (!reader.IsDBNull(8))
            {
                string t = reader.GetString(8);
                if (!String.IsNullOrWhiteSpace(t))
                    r.Tags.AddRange(t.Trim().Split(' '));
            }

            return r;
        }

        public async Task AddWorkspaceResults(Guid workspaceId, IEnumerable<WorkspaceResult> results)
        {
            string updateSql = "set client_encoding='UTF8'; update \"WorkspaceResult\" " +
              "set    uri = :uri, " +
              "       referer = :referer, " +
              "       title = :title, " +
              "       description = :description, " +
              "       page_size = :page_size, " +
              "       updated = :updated, " +
              "       tags = :tags, " +
              "       datahash = :datahash, " +
              "       features = :features, " +
              "       source_server_id = :source_server_id "+
              "where  urihash = :urihash and " +
              "       workspace_id = :workspace_id;";

            string insertSql = "set client_encoding='UTF8'; " +
                "insert into \"WorkspaceResult\" ( urihash, uri, referer, title, description, created, updated, workspace_id, page_size, tags, datahash, features, source_server_id) " +
                "values ( :urihash, :uri, :referer, :title, :description, :created, :updated, :workspace_id, :page_size, :tags, :datahash, :features, :source_server_id );";

            foreach (var result in results)
            {
                NpgsqlCommand cmd = await SetupCommand(updateSql).ConfigureAwait(false);
                _addWorkspaceResultsSetParams(cmd, workspaceId, result);
                int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                if (count == 0) // no record to update
                {
                    cmd = await SetupCommand(insertSql).ConfigureAwait(false);
                    _addWorkspaceResultsSetParams(cmd, workspaceId, result);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        private void _addWorkspaceResultsSetParams(NpgsqlCommand cmd, Guid workspaceId, WorkspaceResult result)
        {
            cmd.Parameters.Add(new NpgsqlParameter<byte[]>("urihash", new MD5Hash(result.UriHash).Values));
            cmd.Parameters.Add(new NpgsqlParameter<string>("uri", result.Uri));
            cmd.Parameters.Add(new NpgsqlParameter<string>("referer", result.RefererUri));
            cmd.Parameters.Add(new NpgsqlParameter<string>("title", result.Title));
            cmd.Parameters.Add(new NpgsqlParameter<string>("description", result.Description));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("created", result.Created));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("updated", result.Updated));
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
            cmd.Parameters.Add(new NpgsqlParameter<long>("page_size", result.PageSize ?? 0));
            cmd.Parameters.Add(new NpgsqlParameter<string>("tags", result.GetTagString()));
            cmd.Parameters.Add(new NpgsqlParameter<byte[]>("datahash", new MD5Hash(result.DataHash).Values));
            cmd.Parameters.Add(new NpgsqlParameter<string[]>("features", result.Features));
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("source_server_id", result.SourceServerId));
            cmd.Prepare();
        }

        public async Task UpdateWorkspaceStatistics()
            => await ExecuteSqlAgainstConnection(
                "update \"Workspace\" " +
                " set   result_count = coalesce((select count(*) c " +
                "                                from   \"WorkspaceResult\" r " +
                "                                where  r.workspace_id = \"Workspace\".workspace_id " +
                "                                group  by workspace_id), 0) "
                );

        public async Task AddWebResourceDataCache(MD5Hash datahash, byte[] data)
        {
            try
            {
                string insertSql = "set client_encoding='UTF8'; insert into \"WebResourceDataCache\" ( datahash, data ) values(:datahash, :data);";

                var cmd = await SetupCommand(insertSql).ConfigureAwait(false);
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("datahash", datahash.Values));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("data", data));
                cmd.Prepare();
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            catch (PostgresException ex)// probably throws when the result already exists
            {
            }
            catch (Exception ex)
            {
                Utility.LogException(ex);
            }
        }

        public async Task<byte[]> GetWebResourceCacheData(MD5Hash datahash)
        {
            string sql = "select datahash, data from \"WebResourceDataCache\" where datahash = :datahash;";

            var cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<byte[]>("datahash", datahash.Values));
            cmd.Prepare();

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                if (reader.Read())
                {
                    long len = reader.GetBytes(1, 0, null, 0, 0);
                    byte[] buffer = new byte[len];
                    reader.GetBytes(1, 0, buffer, 0, buffer.Length);
                    return buffer;
                }
            }

            return null;
        }

        /// <summary>
        /// Create or update a server node in the DB
        /// </summary>
        /// <param name="serverNode"></param>
        /// <returns></returns>
        public async Task<int> SaveServerNode(ServerNode serverNode)
        {
            string updateSql = "set client_encoding='UTF8'; update \"Server\" " +
              "set    name = :name, " +
              "       min_hash = :min_hash, " +
              "       max_hash = :max_hash, " +
              "       approved = :approved, " +
              "       created = :created " +
              "where  server_id = :server_id;";

            string insertSql = "set client_encoding='UTF8'; " +
                "insert into \"Server\" ( server_id, name, min_hash, max_hash, approved, created) " +
                "values ( :server_id, :name, :min_hash, :max_hash, :approved, :created);";

            NpgsqlCommand cmd = await SetupCommand(updateSql).ConfigureAwait(false);
            _saveServerNodeSetParams(cmd, serverNode);
            int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (count == 0) // no record to update
            {
                cmd = await SetupCommand(insertSql).ConfigureAwait(false);
                _saveServerNodeSetParams(cmd, serverNode);
                count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            return count;
        }

        private void _saveServerNodeSetParams(NpgsqlCommand cmd, ServerNode serverNode)
        {
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("server_id", serverNode.ServerId));
            cmd.Parameters.Add(new NpgsqlParameter<string>("name", serverNode.Name));
            cmd.Parameters.Add(new NpgsqlParameter<byte[]>("min_hash", serverNode.UriHashRange.MinHash.Values));
            cmd.Parameters.Add(new NpgsqlParameter<byte[]>("max_hash", serverNode.UriHashRange.MaxHash.Values));
            cmd.Parameters.Add(new NpgsqlParameter<bool>("approved", serverNode.IsApproved));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("created", serverNode.Created));
        }

        public async Task<IEnumerable<ServerNode>> GetServerNodes(string name)
        {
            string sql = "select server_id, name, min_hash, max_hash, approved, created from \"Server\" ";

            if (!String.IsNullOrWhiteSpace(name))
                sql += "where name = :name";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

            if (!String.IsNullOrWhiteSpace(name))
                cmd.Parameters.Add(new NpgsqlParameter<string>("name", name));

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                var l = new List<ServerNode>();
                while (reader.Read())
                {

                    var r = new ServerNode
                    {
                        ServerId = reader.GetGuid(0),
                        Name = reader.GetString(1),
                        UriHashRange = new HashRange(reader.GetMD5Hash(2), reader.GetMD5Hash(3)),
                        IsApproved = reader.GetBoolean(4),
                        Created = reader.GetDateTime(5)
                    };

                    l.Add(r);
                }
                return l;
            }

        }

        async Task ExecuteSqlAgainstConnection(string sql)
        {
            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        T IsNull<T>(DbDataReader dataReader, int ordinal, T defaultValue)
            => dataReader.IsDBNull(ordinal) ? defaultValue : dataReader.GetFieldValue<T>(ordinal);

        /// <summary>
        /// Returns a wait time slightly varied around the timeout to avoid everything ending at once
        /// </summary>
        /// <returns></returns>
        private int GetWaitTime() =>
            random.Next(
                ConnectionPoolWaitTimeInMilliseconds - WaitTimeVarianceInMilliseconds,
                ConnectionPoolWaitTimeInMilliseconds + WaitTimeVarianceInMilliseconds);

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
