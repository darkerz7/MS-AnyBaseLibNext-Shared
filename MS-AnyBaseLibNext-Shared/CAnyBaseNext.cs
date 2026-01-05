namespace MS_AnyBaseLibNext_Shared
{
    public class CAnyBaseNext
    {
        public static class DEFINES
        {
            public static readonly int MaxImportantPerTick = 200;
            public static readonly int MaxImportant = 10000;
            public static readonly int MaxCommonPerTick = 100;
            public static readonly int MaxCommon = 5000;
            public static readonly int DelayQueries = 1000;
            public static readonly int TimeOut = 3000;
        }
        public static IAnyBaseNext Base(string name)
        {
            return name.ToLower() switch
            {
                "sqlite" => new Bases.SQLiteDriver(),
                "mysql" => new Bases.MySQLDriver(),
                "postgre" => new Bases.PostgreDriver(),
                _ => throw new Exception("Unknown DB type"),
            };
        }
    }
}
