
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

namespace Fetcho.Common
{
    /// <summary>
    /// Connect to postgres
    /// </summary>
    public class Database : IDisposable
    {
        public const int ConnectionPoolWaitTimeInMilliseconds = 120000;
        public const int DefaultDatabasePort = 5432;
        public const int MaxConcurrentConnections = 50;
        public const int WaitTimeVarianceInMilliseconds = 250;

        static readonly BinaryFormatter formatter = new BinaryFormatter();
        static readonly ILog log = LogManager.GetLogger(typeof(Database));
        static readonly Random random = new Random(DateTime.Now.Millisecond);
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
            DateTime start = DateTime.Now;
            while (!await connPool.WaitAsync(GetWaitTime()).ConfigureAwait(false))
                log.InfoFormat("Waiting for a database connection for {0}ms",
                    (DateTime.Now - start).TotalMilliseconds);
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
                                                 "is_blocked, last_robots_fetched, robots_file " +
                                                 "from \"Site\" " +
                                                 "where hostname_hash = :hostname_hash"
                                                 );

                cmd.Parameters.AddWithValue("hostname_hash", hash.Values);
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
                            RobotsFile = DeserializeField<RobotsFile>(reader, 4)
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
                const string insert = "insert into \"Site\" (hostname_hash, hostname, is_blocked, last_robots_fetched, robots_file) " +
                  "values (:hostname_hash, :hostname, :is_blocked, :last_robots_fetched, :robots_file)";

                const string update = "update \"Site\" " +
                  "set    hostname = :hostname, " +
                  "       is_blocked = :is_blocked, " +
                  "       last_robots_fetched = :last_robots_fetched, " +
                  "       robots_file = :robots_file " +
                  "where  hostname_hash = :hostname_hash";

                NpgsqlCommand cmd = await SetupCommand(update);

                cmd.Parameters.AddWithValue("hostname_hash", site.Hash.Values);
                cmd.Parameters.AddWithValue("hostname", site.HostName);
                cmd.Parameters.AddWithValue("is_blocked", site.IsBlocked);

                SetBinaryParameter(cmd, "robots_file", site.RobotsFile);

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

        /// <summary>
        /// Returns true if the URI needs visiting
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<bool> NeedsVisiting(Uri uri)
        {
            try
            {
                // the logic here looks backward, but it deals with the case where there's no records!

                var cmd = await SetupCommand("select urihash " +
                                             "from   \"WebResource\" " +
                                             "where  urihash = :urihash " +
                                             "       and next_fetch > :next_fetch " +
                                             "limit  1;");
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("urihash", MD5Hash.Compute(uri).Values));
                cmd.Parameters.Add(new NpgsqlParameter<DateTime>("next_fetch", DateTime.Now));
                cmd.Prepare();

                object o = await cmd.ExecuteScalarAsync();

                if (o == null || o == DBNull.Value)
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                log.ErrorFormat("NeedsVisiting(): {0}", ex);
                return true;
            }
        }

        /// <summary>
        /// Determines if an access key can access a specific workspace
        /// </summary>
        /// <param name="workspaceId"></param>
        /// <returns></returns>
        public async Task<bool> HasWorkspaceAccess(Guid workspaceId, string accountName)
        {
            string sql =
                "select count(*) " +
                "from   \"WorkspaceAccessKey\" " +
                "where  workspace_id = :workspace_id " +
                "       and access_key = :account_name;";

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
                "select workspace_id, name, description, is_active, query_text, result_count, created, is_wellknown " +
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
                        IsWellknown = reader.GetBoolean(7)
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
                "select workspace_id, name, description, is_active, query_text, result_count, created, is_wellknown " +
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
                        IsWellknown = reader.GetBoolean(7)
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
              "       is_wellknown = :is_wellknown " +
              "where  workspace_id = :workspace_id;";

            string insertSql = "set client_encoding='UTF8'; insert into \"Workspace\" ( workspace_id, name, description, query_text, is_active, created, is_wellknown ) " +
              "values ( :workspace_id, :name, :description, :query_text, :is_active, :created, :is_wellknown );";

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
            cmd.Prepare();
        }

        public async Task SaveAccount(Account accessKey)
        {
            string updateSql = "set client_encoding='UTF8'; update \"AccessKey\" " +
              "set    is_active = :is_active " +
              "where  access_key = :access_key;";

            string insertSql = "set client_encoding='UTF8'; insert into \"AccessKey\" ( access_key, is_active, created) " +
              "values ( :access_key, :is_active, :created);";

            NpgsqlCommand cmd = await SetupCommand(updateSql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<string>("access_key", accessKey.Name));
            cmd.Parameters.Add(new NpgsqlParameter<bool>("is_active", accessKey.IsActive));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("created", accessKey.Created));
            int count = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (count == 0) // no record to update
            {
                cmd.CommandText = insertSql;
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<Account> GetAccount(string accessKey)
        {
            string sql = "select access_key, is_active, created from \"AccessKey\" where access_key = :access_key;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<string>("access_key", accessKey));

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

        public async Task DeleteAccount(string accessKey)
        {
            string sql = "delete from \"AccessKey\" where access_key = :access_key;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<string>("access_key", accessKey));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteWorkspaceKey(Guid workspaceAccessKeyId)
        {
            string sql = "delete from \"WorkspaceAccessKey\" where workspace_access_key_id = :workspace_access_key_id;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_access_key_id", workspaceAccessKeyId));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<AccessKey>> GetWorkspaceAccessKeys(Guid workspaceId)
        {
            var l = new List<AccessKey>();


            string sql =
                "select workspace_access_key_id, workspace_id, access_key, is_active, permissions, created, expiry, is_wellknown, name " +
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
                        Permissions = (WorkspaceAccessPermissions)reader.GetInt32(4)
                    });
                }
            }

            return l;
        }

        public async Task<IEnumerable<AccessKey>> GetAccessKeys(string accountName = "", bool includeWorkspace = false)
        {
            var l = new List<AccessKey>();

            string sql =
                "select workspace_access_key_id, workspace_id, access_key, is_active, permissions, created, expiry, is_wellknown, name " +
                "from   \"WorkspaceAccessKey\" ";

            if (!String.IsNullOrWhiteSpace(accountName))
                sql += "where  access_key = :access_key;";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);

            if (!String.IsNullOrWhiteSpace(accountName))
                cmd.Parameters.Add(new NpgsqlParameter<string>("access_key", accountName));

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
                        Permissions = (WorkspaceAccessPermissions)reader.GetInt32(4)
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
                "select workspace_access_key_id, workspace_id, access_key, is_active, permissions, created, expiry, is_wellknown, name " +
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
                        Permissions = (WorkspaceAccessPermissions)reader.GetInt32(4)
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
              "       is_wellknown = :is_wellknown " +
              "where  workspace_id = :workspace_id and access_key = :access_key;";

            string insertSql = "set client_encoding='UTF8'; insert into \"WorkspaceAccessKey\" ( name, workspace_access_key_id, workspace_id, access_key, is_active, permissions, created, expiry, is_wellknown) " +
              "values ( :name, :workspace_access_key_id, :workspace_id, :access_key, :is_active, :permissions, :created, :expiry, :is_wellknown);";

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
            cmd.Parameters.Add(new NpgsqlParameter<string>("access_key", wak.AccountName));
            cmd.Parameters.Add(new NpgsqlParameter<int>("permissions", (int)wak.Permissions));
            cmd.Parameters.Add(new NpgsqlParameter<bool>("is_active", wak.IsActive));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("created", wak.Created));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("expiry", wak.Expiry));
            cmd.Parameters.Add(new NpgsqlParameter<bool>("is_wellknown", wak.IsWellknown));
            cmd.Prepare();
        }

        public async Task<IEnumerable<WorkspaceResult>> GetWorkspaceResults(Guid workspaceId, long offset, int count, bool descendingOrder)
        {
            var l = new List<WorkspaceResult>();

            string sql =
            "select urihash, uri, referrer, title, description, created, page_size, sequence, tags, datahash " +
            "from   \"WorkspaceResult\" " +
            "where  workspace_id = :workspace_id " +
            "order  by sequence " + (descendingOrder ? " desc" : " asc") + " " +
            "limit  " + count + " offset " + offset + ";";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (reader.Read())
                {
                    byte[] urihash = new byte[MD5Hash.ExpectedByteLength];
                    byte[] datahash = new byte[MD5Hash.ExpectedByteLength];

                    reader.GetBytes(0, 0, urihash, 0, MD5Hash.ExpectedByteLength);
                    if (!reader.IsDBNull(9))
                        reader.GetBytes(9, 0, datahash, 0, MD5Hash.ExpectedByteLength);

                    var h = new MD5Hash(datahash);

                    l.Add(new WorkspaceResult()
                    {
                        UriHash = new MD5Hash(urihash).ToString(),
                        Uri = reader.GetString(1),
                        RefererUri = IsNull(reader, 2, ""),
                        Title = IsNull(reader, 3, ""),
                        Description = IsNull(reader, 4, ""),
                        Created = reader.GetDateTime(5),
                        PageSize = reader.GetInt64(6),
                        GlobalSequence = reader.GetInt64(7),
                        DataHash = h == MD5Hash.Empty ? "" : h.ToString()
                    });

                    if (!reader.IsDBNull(8))
                    {
                        string t = reader.GetString(8);
                        if (!String.IsNullOrWhiteSpace(t))
                            l[l.Count - 1].Tags.AddRange(t.Trim().Split(' '));
                    }
                }
            }

            return l;
        }

        public async Task<IEnumerable<WorkspaceResult>> GetWorkspaceResultsByRandom(Guid workspaceId, int count)
        {
            var l = new List<WorkspaceResult>();
            byte[] urihash = new byte[MD5Hash.ExpectedByteLength];
            byte[] datahash = new byte[MD5Hash.ExpectedByteLength];

            string sql =
            "select urihash, uri, referrer, title, description, created, page_size, sequence, tags, datahash " +
            "from   \"WorkspaceResult\" " +
            "where  workspace_id = :workspace_id and" +
            "       random > :random " +
            "order  by random " +
            "limit  " + count + ";";

            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
            cmd.Parameters.Add(new NpgsqlParameter<double>("random", random.NextDouble()));

            using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (reader.Read())
                {
                    reader.GetBytes(0, 0, urihash, 0, MD5Hash.ExpectedByteLength);
                    if (!reader.IsDBNull(9))
                        reader.GetBytes(9, 0, datahash, 0, MD5Hash.ExpectedByteLength);

                    var h = new MD5Hash(datahash);

                    l.Add(new WorkspaceResult()
                    {
                        UriHash = new MD5Hash(urihash).ToString(),
                        Uri = reader.GetString(1),
                        RefererUri = IsNull(reader, 2, ""),
                        Title = IsNull(reader, 3, ""),
                        Description = IsNull(reader, 4, ""),
                        Created = reader.GetDateTime(5),
                        PageSize = reader.GetInt64(6),
                        GlobalSequence = reader.GetInt64(7),
                        DataHash = h == MD5Hash.Empty ? "" : h.ToString()
                    });

                    if (!reader.IsDBNull(8))
                    {
                        string t = reader.GetString(8);
                        if (!String.IsNullOrWhiteSpace(t))
                            l[l.Count - 1].Tags.AddRange(t.Trim().Split(' '));
                    }
                }
            }

            return l;
        }

        public async Task AddWorkspaceResults(Guid workspaceId, IEnumerable<WorkspaceResult> results)
        {
            string updateSql = "set client_encoding='UTF8'; update \"WorkspaceResult\" " +
              "set    uri = :uri, " +
              "       referrer = :referrer, " +
              "       title = :title, " +
              "       description = :description, " +
              "       page_size = :page_size, " +
              "       created = :created, " +
              "       tags = :tags, " +
              "       datahash = :datahash " +
              "where  urihash = :urihash and " +
              "       workspace_id = :workspace_id;";

            string insertSql = "set client_encoding='UTF8'; insert into \"WorkspaceResult\" ( urihash, uri, referrer, title, description, created, workspace_id, page_size, tags, datahash) " +
              "values ( :urihash, :uri, :referrer, :title, :description, :created, :workspace_id, :page_size, :tags, :datahash );";

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
            cmd.Parameters.Add(new NpgsqlParameter<string>("referrer", result.RefererUri));
            cmd.Parameters.Add(new NpgsqlParameter<string>("title", result.Title));
            cmd.Parameters.Add(new NpgsqlParameter<string>("description", result.Description));
            cmd.Parameters.Add(new NpgsqlParameter<DateTime>("created", result.Created));
            cmd.Parameters.Add(new NpgsqlParameter<Guid>("workspace_id", workspaceId));
            cmd.Parameters.Add(new NpgsqlParameter<long>("page_size", result.PageSize ?? 0));
            cmd.Parameters.Add(new NpgsqlParameter<string>("tags", result.GetTagString()));
            cmd.Parameters.Add(new NpgsqlParameter<byte[]>("datahash", new MD5Hash(result.DataHash).Values));
            cmd.Prepare();
        }

        public async Task UpdateWorkspaceStatistics()
        {
            await ExecuteSqlAgainstConnection(
                "update \"Workspace\" " +
                " set   result_count = coalesce((select count(*) c " +
                "                                from   \"WorkspaceResult\" r " +
                "                                where  r.workspace_id = \"Workspace\".workspace_id " +
                "                                group  by workspace_id), 0) "
                );
        }

        public async Task AddWebResourceDataCache(MD5Hash datahash, MemoryStream stream)
        {
            try
            {
                string insertSql = "set client_encoding='UTF8'; insert into \"WebResourceDataCache\" ( datahash, data ) values(:datahash, :data);";

                var cmd = await SetupCommand(insertSql).ConfigureAwait(false);
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("datahash", datahash.Values));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]>("data", stream.GetBuffer()));
                cmd.Prepare();
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Utility.LogException(ex); // probably throws when the result already exists
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

        async Task ExecuteSqlAgainstConnection(string sql)
        {
            NpgsqlCommand cmd = await SetupCommand(sql).ConfigureAwait(false);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
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
                var o = formatter.Deserialize(ms) as T;
                if (o == null) throw new FetchoException(string.Format("Deserialization to {0} failed", typeof(T)));
                return o;
            }
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
