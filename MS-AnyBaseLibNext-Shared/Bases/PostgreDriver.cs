using Npgsql;
using System.Data;
using System.Timers;

namespace MS_AnyBaseLibNext_Shared.Bases
{
    internal class PostgreDriver : IAnyBaseNext
    {
        private string? ConnStr;
        private readonly List<QueryObject> ImportantQueries = [];
        private readonly List<QueryObject> CommonQueries = [];
        private System.Timers.Timer? aTimer;
        private ConnectionState LastState = ConnectionState.Closed;

        public void Set(string db_name, string db_host, string db_user = "", string db_pass = "")
        {
            UnSet();
            var db_host_arr = db_host.Split(":");
            var db_server = db_host_arr[0];
            int db_port = 3306;
            if (db_host_arr.Length > 1)
                db_port = int.Parse(db_host_arr[1]);

            ConnStr = new NpgsqlConnectionStringBuilder
            {
                Host = db_server,
                Database = db_name,
                Username = db_user,
                Password = db_pass,
                SslMode = SslMode.Prefer,
                Port = db_port

            }.ConnectionString;

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
            if (important) ImportantQueries.Add(new QueryObject(Common.PrepareClear(q, args), action, non_query));
            else CommonQueries.Add(new QueryObject(Common.PrepareClear(q, args), action, non_query));
        }

        public ConnectionState GetLastState()
        {
            return LastState;
        }

        private async Task<NpgsqlConnection?> CreateConnection()
        {
            if (string.IsNullOrWhiteSpace(ConnStr)) return null;

            try
            {
                var conn = new NpgsqlConnection(ConnStr);
                await conn.OpenAsync();
                return conn;
            }
            catch { return null; }
        }

        private async Task<NpgsqlConnection?> GetConnection()
        {
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
    }
}
