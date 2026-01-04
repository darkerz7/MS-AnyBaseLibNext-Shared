using MySqlConnector;
using System.Collections.Concurrent;
using System.Data;

namespace MS_AnyBaseLibNext_Shared.Bases
{
    internal class MySQLDriver : IAnyBaseNext
    {
        private string? ConnStr;
        private readonly ConcurrentQueue<QueryObject> ImportantQueries = [];
        private readonly ConcurrentQueue<QueryObject> CommonQueries = [];
        private PeriodicTimer? _timer;
        private CancellationTokenSource? _cts;
        private ConnectionState LastState = ConnectionState.Closed;

        public void Set(string db_name, string db_host, string db_user = "", string db_pass = "")
        {
            UnSet();
            var db_host_arr = db_host.Split(":");
            var db_server = db_host_arr[0];
            uint db_port = db_host_arr.Length > 1 ? uint.Parse(db_host_arr[1]) : 3306;

            ConnStr = new MySqlConnectionStringBuilder
            {
                Server = db_server,
                Database = db_name,
                UserID = db_user,
                Password = db_pass,
                SslMode = MySqlSslMode.None,
                Port = db_port,
                AllowPublicKeyRetrieval = true,
                Pooling = true

            }.ConnectionString;

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
            var qo = new QueryObject(Common.PrepareClear(q, args), action, non_query);
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
                    using var conn = new MySqlConnection(ConnStr);
                    await conn.OpenAsync(ct);

                    LastState = conn.State;

                    await Common.ExecuteQuery(conn, ImportantQueries, CommonQueries);
                }
                catch (Exception) { LastState = ConnectionState.Broken; }
            }
        }
    }
}
