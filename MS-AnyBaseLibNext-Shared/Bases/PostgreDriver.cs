using Npgsql;
using System.Data;
using System.Threading.Channels;

namespace MS_AnyBaseLibNext_Shared.Bases
{
    internal class PostgreDriver : IAnyBaseNext
    {
        private string? ConnStr;
        private readonly Channel<QueryObject> ImportantQueries;
        private readonly Channel<QueryObject> CommonQueries;
        private CancellationTokenSource? _cts;
        private Task? _workerTask;
        private readonly Lock _lockObj = new();
        private volatile ConnectionState LastState = ConnectionState.Closed;

        public PostgreDriver()
        {
            ImportantQueries = Channel.CreateBounded<QueryObject>(new BoundedChannelOptions(CAnyBaseNext.DEFINES.MaxImportant) { FullMode = BoundedChannelFullMode.DropWrite });
            CommonQueries = Channel.CreateBounded<QueryObject>(new BoundedChannelOptions(CAnyBaseNext.DEFINES.MaxCommon) { FullMode = BoundedChannelFullMode.DropWrite });
        }

        public void Set(string db_name, string db_host, string db_user = "", string db_pass = "")
        {
            lock (_lockObj)
            {
                UnSet();
                string db_server = db_host;
                int db_port = 5432;

                if (db_host.Contains(':'))
                {
                    var prefix = db_host.Contains("://") ? "" : "postgre://";
                    if (Uri.TryCreate(prefix + db_host, UriKind.Absolute, out var uri))
                    {
                        db_server = uri.Host;
                        db_port = uri.Port <= 0 ? 5432 : uri.Port;
                    }
                }

                ConnStr = new NpgsqlConnectionStringBuilder
                {
                    Host = db_server,
                    Database = db_name,
                    Username = db_user,
                    Password = db_pass,
                    SslMode = SslMode.Prefer,
                    Port = db_port

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
            var qo = new QueryObject(q, args, action, non_query);
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
                    using var conn = new NpgsqlConnection(ConnStr);

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
    }
}
