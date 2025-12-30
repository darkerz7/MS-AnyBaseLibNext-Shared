using System.Data.Common;

namespace MS_AnyBaseLibNext_Shared.Bases
{
    internal static class Common
    {
        public static void ExecuteQuery(DbConnection? conn, List<QueryObject> ImportantQueries, List<QueryObject> CommonQueries)
        {
            if (conn == null)
            {
                CommonQueries.Clear();
                return;
            }

            List<QueryObject> ImportantQueriesDublicate = [.. ImportantQueries];
            ImportantQueries.Clear();
            List<QueryObject> CommonQueriesDublicate = [.. CommonQueries];
            CommonQueries.Clear();

            foreach (var iq in ImportantQueriesDublicate)
            {
                var t = Task.Run(async () => QueryAsync(conn, iq));
                t.Wait();
            }

            foreach (var cq in CommonQueriesDublicate)
            {
                var t = Task.Run(async () => QueryAsync(conn, cq));
                t.Wait();
            }

            conn.Close();
        }

        async static void QueryAsync(DbConnection conn, QueryObject qo)
        {
            List<List<string?>>? list = [];
            var sql = conn.CreateCommand();
            sql.CommandText = qo.sQuery;
            var query = Task.Run(async () =>
            {
                if (qo.bNonQuery) await sql.ExecuteNonQueryAsync();
                else
                {
                    try
                    {
                        using DbDataReader reader = await sql.ExecuteReaderAsync();
                        {
                            while (await reader.ReadAsync())
                            {
                                var fields = new List<string?>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    if (!reader.IsDBNull(i)) fields.Add(reader.GetValue(i).ToString());
                                    else fields.Add(null);
                                }
                                list.Add(fields);
                            }
                        }
                    }
                    catch { list = []; }
                }
            });
            query.Wait();
            var task = new Task<List<List<string?>>?>(() => list);
            if (qo.lAction != null) _ = task.ContinueWith(obj => qo.lAction(obj.Result));
            task.Start();
        }

        private static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0) return text;
            return text[..pos] + replace + text[(pos + search.Length)..];
        }

        public static string PrepareArg(string arg)
        {
            if (arg == null) return "";
            string[] escapes = ["\\", "'", "\"", "`", "%"];
            var new_arg = "";
            foreach (var ch in arg)
            {
                if (escapes.Contains(ch.ToString())) new_arg += "\\";
                new_arg += ch.ToString();
            }

            return new_arg;
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
