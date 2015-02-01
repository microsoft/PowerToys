
namespace Wox.Core.Plugin.QueryDispatcher
{
    internal static class QueryDispatcher
    {
        private static readonly IQueryDispatcher UserPluginDispatcher = new UserPluginQueryDispatcher();
        private static readonly IQueryDispatcher SystemPluginDispatcher = new SystemPluginQueryDispatcher();

        public static void Dispatch(Wox.Plugin.Query query)
        {
            if (PluginManager.IsUserPluginQuery(query))
            {
                query.Search = query.RawQuery.Substring(query.RawQuery.IndexOf(' ') + 1);
                UserPluginDispatcher.Dispatch(query);
            }
            else
            {
                query.Search = query.RawQuery;
                SystemPluginDispatcher.Dispatch(query);
            }
        }
    }
}
