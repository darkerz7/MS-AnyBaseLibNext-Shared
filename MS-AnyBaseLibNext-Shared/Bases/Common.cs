using System.Collections.Concurrent;
using System.Data.Common;

namespace MS_AnyBaseLibNext_Shared.Bases
{
    internal static class Common
    {
        public static async Task ExecuteQuery(DbConnection? conn, ConcurrentQueue<QueryObject> ImportantQueries, ConcurrentQueue<QueryObject> CommonQueries)
        {
            if (conn == null)
            {
                CommonQueries.Clear();
                return;
            }

            var batch = new List<QueryObject>();
            while (ImportantQueries.TryDequeue(out var qo)) batch.Add(qo);
            while (CommonQueries.TryDequeue(out var qo)) batch.Add(qo);

            foreach (var qo in batch) await QueryAsync(conn, qo);

            await conn.CloseAsync();
        }

        async static Task QueryAsync(DbConnection conn, QueryObject qo)
        {
            try
            {
                List<List<string?>>? list = [];
                using var sql = conn.CreateCommand();
                sql.CommandText = qo.sQuery;
                if (qo.bNonQuery)
                {
                    await sql.ExecuteNonQueryAsync();
                    qo.lAction?.Invoke([]);
                }
                else
                {
                    var results = new List<List<string?>>();
                    using var reader = await sql.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var row = new List<string?>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i).ToString());
                        }
                        results.Add(row);
                    }
                    qo.lAction?.Invoke(results);
                }
            }
            catch { qo.lAction?.Invoke([]); }
        }

        private static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0) return text;
            return text[..pos] + replace + text[(pos + search.Length)..];
        }

        public static string PrepareArg(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "";
            var sb = new System.Text.StringBuilder(arg.Length + 5);
            foreach (var ch in arg)
            {
                if ("\\'\"`%".Contains(ch)) sb.Append('\\');
                sb.Append(ch);
            }
            return sb.ToString();
        }

        public static string PrepareClear(string q, List<string>? args, Func<string, string>? escape_func = null)
        {
            var new_q = q;
            if (args != null)
                foreach (string arg in args.ToList())
                {
                    var new_q2 = "";
                    if (escape_func == null) new_q2 = ReplaceFirst(new_q, "{ARG}", PrepareArg(arg));
                    else new_q2 = ReplaceFirst(new_q, "{ARG}", escape_func(arg));

                    if (new_q2 == new_q) throw new Exception("Mailformed query [Too many args in params]");
                    new_q = new_q2;
                }
            if (new_q.Contains("{ARG}")) throw new Exception("Mailformed query [Not enough args in params]");
            return new_q;
        }
    }

    class QueryObject(string q, Action<List<List<string?>>?>? action = null, bool non_query = false)
    {
        public readonly string sQuery = q;
        public Action<List<List<string?>>?>? lAction = action;
        public readonly bool bNonQuery = non_query;
    }
}
