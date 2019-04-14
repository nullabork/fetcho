using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Fetcho.Common
{
    public class DatabasePool
    {
        private static readonly object databasePoolLock = new object();
        private static BufferBlock<Database> databasePool = null;

        public static int Count { get => databasePool.Count; }

        public static int WaitingForDatabase { get => _waitingForDatabase; }
        private static int _waitingForDatabase;

        public static void Initialise()
        {
            lock (databasePoolLock)
            {
                databasePool = new BufferBlock<Database>();
                for (int i = 0; i < Database.MaxConcurrentConnections - 5; i++)
                    databasePool.SendAsync(CreateDatabase().GetAwaiter().GetResult());
            }
        }

        private static async Task<Database> CreateDatabase()
        {
            var db = new Database();
            await db.Open();
            return db;
        }

        public static async Task<Database> GetDatabaseAsync()
        {
            Interlocked.Increment(ref _waitingForDatabase);
            var db = await databasePool.ReceiveAsync().ConfigureAwait(false);
            Interlocked.Decrement(ref _waitingForDatabase);
            return db;
        }

        public static async Task GiveBackToPool(Database db) => await databasePool.SendOrWaitAsync(db);

    }

}
