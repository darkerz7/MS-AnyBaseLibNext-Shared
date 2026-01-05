using Microsoft.Data.Sqlite;
using System.Data;
using System.Threading.Channels;

namespace MS_AnyBaseLibNext_Shared.Bases
{
    internal class SQLiteDriver : IAnyBaseNext
    {
        private string? ConnStr;
        private readonly Channel<QueryObject> ImportantQueries;
        private readonly Channel<QueryObject> CommonQueries;
        private CancellationTokenSource? _cts;
        private Task? _workerTask;
        private readonly Lock _lockObj = new();
        private volatile ConnectionState LastState = ConnectionState.Closed;
        private bool bInitialized = false;

        public SQLiteDriver()
        {
            ImportantQueries = Channel.CreateBounded<QueryObject>(new BoundedChannelOptions(CAnyBaseNext.DEFINES.MaxImportant) { FullMode = BoundedChannelFullMode.DropWrite });
            CommonQueries = Channel.CreateBounded<QueryObject>(new BoundedChannelOptions(CAnyBaseNext.DEFINES.MaxCommon) { FullMode = BoundedChannelFullMode.DropWrite });
        }

        public void Set(string db_name, string db_host = "", string db_user = "", string db_pass = "")
        {
            if (!bInitialized)
            {
                SQLitePCL.Batteries.Init();
                bInitialized = true;
            }
            lock (_lockObj)
            {
                UnSet();

                ConnStr = new SqliteConnectionStringBuilder
                {
                    DataSource = $"{db_name}.sqlite",
                    Pooling = true

                }.ConnectionString;

                _cts = new CancellationTokenSource();
                _workerTask = WorkerLoop(_cts.Token);
            }
        }

        public void UnSet()
        {
            lock (_lockObj)
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    try { _workerTask?.Wait(1000); } catch { }
                    _cts.Dispose();
                    _cts = null;
                    _workerTask = null;
                }
                LastState = ConnectionState.Closed;
            }
        }

        public void QueryAsync(string q, List<string>? args, Action<List<List<string?>>?>? action = null, bool non_query = false, bool important = false)
        {
            var qo = new QueryObject(Common.RemoveTypeCasts(FixForSQLite(q)), args, action, non_query);
            var channel = important ? ImportantQueries.Writer : CommonQueries.Writer;
            if (!channel.TryWrite(qo)) Common.SafeInvoke(qo, null);
        }

        public ConnectionState GetLastState() => LastState;

        private async Task WorkerLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(CAnyBaseNext.DEFINES.DelayQueries, ct);

                    if (string.IsNullOrEmpty(ConnStr)) continue;
                    using var conn = new SqliteConnection(ConnStr);

                    await conn.OpenAsync(ct);
                    LastState = conn.State;

                    await Common.ExecuteQuery(conn, ImportantQueries.Reader, CommonQueries.Reader, ct);

                    if (conn.State != ConnectionState.Open) throw new Exception("Connection lost during query execution");

                    LastState = conn.State;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception)
                {
                    LastState = ConnectionState.Broken;
                    Common.ClearQueries(CommonQueries.Reader);

                    try { await Task.Delay(CAnyBaseNext.DEFINES.TimeOut, ct); }
                    catch (OperationCanceledException) { break; }
                }
            }
            LastState = ConnectionState.Closed;
        }

        private static string FixForSQLite(string q)
        {
            return q.Replace("PRIMARY KEY AUTO_INCREMENT", "PRIMARY KEY AUTOINCREMENT").Replace("UNIX_TIMESTAMP()", "UNIXEPOCH()");
        }
    }
}
