namespace MS_AnyBaseLibNext_Shared
{
    public class CAnyBaseNext
    {
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
