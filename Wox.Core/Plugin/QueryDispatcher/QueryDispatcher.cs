
namespace Wox.Core.Plugin.QueryDispatcher
{
    internal static class QueryDispatcher
    {
        private static IQueryDispatcher pluginCmd = new UserPluginQueryDispatcher();
        private static IQueryDispatcher systemCmd = new SystemPluginQueryDispatcher();

        public static void Dispatch(Wox.Plugin.Query query)
        {
            if (PluginManager.IsUserPluginQuery(query))
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
