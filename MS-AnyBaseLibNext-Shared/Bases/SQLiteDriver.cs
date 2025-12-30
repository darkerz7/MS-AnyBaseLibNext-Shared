using Microsoft.Data.Sqlite;
using System.Data;
using System.Timers;

namespace MS_AnyBaseLibNext_Shared.Bases
{
    internal class SQLiteDriver : IAnyBaseNext
    {
        private string? ConnStr;
        private readonly List<QueryObject> ImportantQueries = [];
        private readonly List<QueryObject> CommonQueries = [];
        private System.Timers.Timer? aTimer;
        private ConnectionState LastState = ConnectionState.Closed;
        private bool bOnce = true;

        public void Set(string db_name, string db_host = "", string db_user = "", string db_pass = "")
        {
            UnSet();
            ConnStr = $"Data Source={db_name}.sqlite;";

            aTimer = new System.Timers.Timer(1000);
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        public void UnSet()
        {
            aTimer?.Stop();
            aTimer?.Dispose();
            ImportantQueries.Clear();
            CommonQueries.Clear();
        }

        public void QueryAsync(string q, List<string>? args, Action<List<List<string?>>?>? action = null, bool non_query = false, bool important = false)
        {
            if (important) ImportantQueries.Add(new QueryObject(Common.PrepareClear(FixForSQLite(q), args), action, non_query));
            else CommonQueries.Add(new QueryObject(Common.PrepareClear(FixForSQLite(q), args), action, non_query));
        }

        public ConnectionState GetLastState()
        {
            return LastState;
        }

        private async Task<SqliteConnection?> CreateConnection()
        {
            if (string.IsNullOrWhiteSpace(ConnStr)) return null;

            try
            {
                var conn = new SqliteConnection(ConnStr);
                await conn.OpenAsync();
                return conn;
            }
            catch { return null; }
        }

        private async Task<SqliteConnection?> GetConnection()
        {
            if (bOnce)
            {
                SQLitePCL.Batteries.Init();
                bOnce = false;
            }
            var conn = await CreateConnection();
            int wait_opened = 10;
            while (wait_opened > 0)
            {
                if (conn != null && conn.State == ConnectionState.Open) break;
                wait_opened--;
                conn?.Close();
                conn = await CreateConnection();
                Task.Delay(150).Wait();
            }
            LastState = conn != null ? conn.State : ConnectionState.Closed;
            if (wait_opened == 0) return null;

            return conn;
        }

        private async void OnTimedEvent(object? sender, ElapsedEventArgs e)
        {
            Common.ExecuteQuery(await GetConnection(), ImportantQueries, CommonQueries);
        }

        private static string FixForSQLite(string q)
        {
            return q.Replace("PRIMARY KEY AUTO_INCREMENT", "PRIMARY KEY AUTOINCREMENT").Replace("UNIX_TIMESTAMP()", "UNIXEPOCH()");
        }
    }
}
