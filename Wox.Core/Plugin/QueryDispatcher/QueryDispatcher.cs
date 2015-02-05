
using System.Threading;
using Wox.Plugin;

namespace Wox.Core.Plugin.QueryDispatcher
{
    internal static class QueryDispatcher
    {
        private static readonly IQueryDispatcher UserPluginDispatcher = new UserPluginQueryDispatcher();
        private static readonly IQueryDispatcher SystemPluginDispatcher = new SystemPluginQueryDispatcher();

        public static void Dispatch(Wox.Plugin.Query query)
        {
            PluginPair exclusiveSearchPlugin = PluginManager.GetExclusiveSearchPlugin(query);
            if (exclusiveSearchPlugin != null)
            {
                ThreadPool.QueueUserWorkItem(state =>
                {
                    PluginManager.ExecutePluginQuery(exclusiveSearchPlugin, query);
                });
                return;
            }

            if (PluginManager.IsUserPluginQuery(query))
            {
                UserPluginDispatcher.Dispatch(query);
            }
            else
            {
                SystemPluginDispatcher.Dispatch(query);
            }
        }
    }
}
