
namespace Wox.Core.Plugin.QueryDispatcher
{
    internal static class QueryDispatcher
    {
        private static IQueryDispatcher regularDispatcher = new RegularPluginQueryDispatcher();
        private static IQueryDispatcher wildcardDispatcher = new WildcardPluginQueryDispatcher();

        public static void Dispatch(Wox.Plugin.Query query)
        {
            if (PluginManager.IsRegularPluginQuery(query))
            {
                regularDispatcher.Dispatch(query);
            }
            else
            {
                wildcardDispatcher.Dispatch(query);                
            }
        }
    }
}
