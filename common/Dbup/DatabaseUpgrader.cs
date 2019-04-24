using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DbUp;

namespace Fetcho.Common.Dbup
{
    public static class DatabaseUpgrader
    {
        public static void Upgrade()
        {
            ThrowIfConnectionStringNotConfigured();
            string connectionString = ConfigurationManager.ConnectionStrings["db"]?.ToString();

            var upgrader =
                DeployChanges.To
                    .PostgresqlDatabase(connectionString)
                    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                    .WithTransaction()
                    .LogToConsole()
                    .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                throw new FetchoException("Database upgrade failed");
            }
        }

        public static void ThrowIfConnectionStringNotConfigured()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db"]?.ToString();
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new FetchoException("No connection string specified");

        }
    }
}
