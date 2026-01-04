using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Data;

namespace MS_AnyBaseLibNext_Shared.Bases
{
    internal class SQLiteDriver : IAnyBaseNext
    {
        private string? ConnStr;
        private readonly ConcurrentQueue<QueryObject> ImportantQueries = [];
        private readonly ConcurrentQueue<QueryObject> CommonQueries = [];
        private PeriodicTimer? _timer;
        private CancellationTokenSource? _cts;
        private ConnectionState LastState = ConnectionState.Closed;
        private bool bInitialized = false;

        public void Set(string db_name, string db_host = "", string db_user = "", string db_pass = "")
        {
            UnSet();

            if (!bInitialized)
            {
                SQLitePCL.Batteries.Init();
                bInitialized = true;
            }

            ConnStr = $"Data Source={db_name}.sqlite;Journal Mode=Wal;";

            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            _ = OnTimedEvent(_cts.Token);
        }

        public void UnSet()
        {
            _cts?.Cancel();
            _timer?.Dispose();
            ImportantQueries.Clear();
            CommonQueries.Clear();
        }

        public void QueryAsync(string q, List<string>? args, Action<List<List<string?>>?>? action = null, bool non_query = false, bool important = false)
        {
            var fixedQuery = FixForSQLite(q);
            var qo = new QueryObject(Common.PrepareClear(fixedQuery, args), action, non_query);
            if (important) ImportantQueries.Enqueue(qo);
            else CommonQueries.Enqueue(qo);
        }

        public ConnectionState GetLastState() => LastState;

        private async Task OnTimedEvent(CancellationToken ct)
        {
            while (await _timer!.WaitForNextTickAsync(ct))
            {
                //if (ImportantQueries.IsEmpty && CommonQueries.IsEmpty) continue;

                try
                {
                    using var conn = new SqliteConnection(ConnStr);
                    await conn.OpenAsync(ct);
                    LastState = conn.State;

                    await Common.ExecuteQuery(conn, ImportantQueries, CommonQueries);
                }
                catch { LastState = ConnectionState.Broken; }
            }
        }

        private static string FixForSQLite(string q)
        {
            return q.Replace("PRIMARY KEY AUTO_INCREMENT", "PRIMARY KEY AUTOINCREMENT").Replace("UNIX_TIMESTAMP()", "UNIXEPOCH()");
        }
    }
}
