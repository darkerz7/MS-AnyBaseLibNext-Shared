using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace MS_AnyBaseLibNext_Shared.Bases
{
    internal static partial class Common
    {
        public static async Task ExecuteQuery(DbConnection conn, ChannelReader<QueryObject> ImportantQueries, ChannelReader<QueryObject> CommonQueries, CancellationToken ct)
        {
            int iLimitImportant = CAnyBaseNext.DEFINES.MaxImportantPerTick;
            while (iLimitImportant-- > 0 && ImportantQueries.TryRead(out var qo))
            {
                if (ct.IsCancellationRequested) break;
                await QueryAsync(conn, qo, ct);
                if (conn.State != ConnectionState.Open) return;
            }

            int iLimitCommon = CAnyBaseNext.DEFINES.MaxCommonPerTick;
            while (iLimitCommon-- > 0 && CommonQueries.TryRead(out var qo))
            {
                if (ct.IsCancellationRequested) break;
                await QueryAsync(conn, qo, ct);
                if (conn.State != ConnectionState.Open) return;
            }
        }

        public static void ClearQueries(ChannelReader<QueryObject> channel)
        {
            while (channel.TryRead(out var qo)) { SafeInvoke(qo, null); }
        }

        async static Task QueryAsync(DbConnection conn, QueryObject qo, CancellationToken ct)
        {
            try
            {
                using var sql = conn.CreateCommand();
                if (PrepareQuery(sql, qo.sQuery, qo.lArgs) == QueryClearResult.Success)
                {
                    if (qo.bNonQuery)
                    {
                        await sql.ExecuteNonQueryAsync(ct);
                        SafeInvoke(qo, []);
                    }
                    else
                    {
                        var results = new List<List<string?>>();
                        using var reader = await sql.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
                        while (await reader.ReadAsync(ct))
                        {
                            int fieldCount = reader.FieldCount;
                            var row = new List<string?>(fieldCount);

                            for (int i = 0; i < fieldCount; i++)
                            {
                                if (await reader.IsDBNullAsync(i, ct)) row.Add(null);
                                else
                                {
                                    object val = await reader.GetFieldValueAsync<object>(i, ct);

                                    row.Add(val switch
                                    {
                                        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                                        DBNull => null,
                                        _ => val.ToString()
                                    });
                                }
                            }
                            results.Add(row);
                        }
                        SafeInvoke(qo, results);
                    }

                }
                else SafeInvoke(qo, null);
            }
            catch (DbException ex) when (IsCriticalError(ex))
            {
                SafeInvoke(qo, null);
                throw;
            }
            catch
            {
                SafeInvoke(qo, null);
            }
        }

        private static bool IsCriticalError(DbException ex) => ex.SqlState == "08S01" || ex.Message.Contains("Connection");

        public static void SafeInvoke(QueryObject qo, List<List<string?>>? data)
        {
            if (qo.lAction == null) return;
            _ = Task.Run(() => {
                try { qo.lAction?.Invoke(data); }
                catch { }
            }, CancellationToken.None);
        }

        public static QueryClearResult PrepareQuery(DbCommand sql, string q, List<string>? args)
        {
            int iCountArgsRegex = ARGRegex().Count(q);
            int iCountArgs = args?.Count ?? 0;
            if (iCountArgsRegex != iCountArgs) return iCountArgsRegex > iCountArgs ? QueryClearResult.NotEnoughArg : QueryClearResult.ManyArgs;

            if (args != null && iCountArgs > 0)
            {
                int index = 0;
                sql.CommandText = ARGRegex().Replace(q, m => $"@p_anybaselibnext{index++}");
                for (int i = 0; i < iCountArgs; i++)
                {
                    DbParameter par = sql.CreateParameter();
                    par.ParameterName = $"@p_anybaselibnext{i}";
                    par.Value = (object?)args[i] ?? DBNull.Value;
                    par.DbType = DbType.String;
                    sql.Parameters.Add(par);
                }
            }
            else sql.CommandText = q;
            return QueryClearResult.Success;
        }

        public static string RemoveTypeCasts(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return TypeCasts().Replace(input, "{ARG}");
        }

        [GeneratedRegex("{ARG}")]
        private static partial Regex ARGRegex();

        [GeneratedRegex(@"(\{ARG\})(::[a-zA-Z0-9_""\[\]]+)+")]
        private static partial Regex TypeCasts();
    }

    enum QueryClearResult { Success, ManyArgs, NotEnoughArg }

    class QueryObject(string q, List<string>? args, Action<List<List<string?>>?>? action = null, bool non_query = false)
    {
        public readonly string sQuery = q;
        public readonly List<string>? lArgs = args;
        public readonly Action<List<List<string?>>?>? lAction = action;
        public readonly bool bNonQuery = non_query;
    }
}
