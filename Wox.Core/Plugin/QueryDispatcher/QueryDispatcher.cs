
namespace Wox.Core.Plugin.QueryDispatcher
{
    internal static class QueryDispatcher
    {
        private static IQueryDispatcher pluginCmd = new RegularPluginQueryDispatcher();
        private static IQueryDispatcher systemCmd = new WildcardPluginQueryDispatcher();

        public static void Dispatch(Wox.Plugin.Query query)
        {
            if (PluginManager.IsRegularPluginQuery(query))
            {
                pluginCmd.Dispatch(query);
            }
            else
            {
                systemCmd.Dispatch(query);                
            }
        }
    }
}
