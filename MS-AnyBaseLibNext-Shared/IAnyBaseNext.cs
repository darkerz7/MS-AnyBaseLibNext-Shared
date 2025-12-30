using System.Data;

namespace MS_AnyBaseLibNext_Shared
{
    public interface IAnyBaseNext
    {
        const string Identity = nameof(IAnyBaseNext);
        /**
         * Called when it is necessary to add or change the database configuration
         * 
         * @param db_name		Database name/Database file name (SQLite)
         * @param db_host       Host name or IP with port (ex. 127.0.0.1:3306)
         * @param db_user       Database user name
         * @param db_pass       Database user password
        */
        public void Set(string db_name, string db_host = "", string db_user = "", string db_pass = "");

        /**
         * Called when it is necessary to terminate the connection to the database and clear queries (ex. plugin unload)
         */
        public void UnSet();

        /**
         * Called to execute queries. Within 1 second, it collects a list of queries and executes it in a single connection
         * 
         * @param q             The required database query ({ARG} - parameter)
         * @param args          List of arguments to substitute into the query instead of {ARG}
         * @param action        Performing actions upon completion of a database query (result or other actions)
         * @param non_query     Query type (false for SELECT)
         * @param important     Executing a query when reconnecting after a connection loss
         */
        public void QueryAsync(string q, List<string>? args, Action<List<List<string?>>?>? action = null, bool non_query = false, bool important = false);

        /**
         * Called to check the connection state
         * 
         * @result ConnectionState      Shows the latest connection state (ConnectionState.Open - you can execute queries)
         */
        public ConnectionState GetLastState();
    }
}
