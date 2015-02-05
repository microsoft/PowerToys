
using System.Threading;
using Wox.Plugin;

namespace Wox.Core.Plugin.QueryDispatcher
{
    internal static class QueryDispatcher
    {
        private static readonly IQueryDispatcher exclusivePluginDispatcher = new ExclusiveQueryDispatcher();
        private static readonly IQueryDispatcher genericQueryDispatcher = new GenericQueryDispatcher();

        public static void Dispatch(Query query)
        {
            if (PluginManager.IsExclusivePluginQuery(query))
            {
                exclusivePluginDispatcher.Dispatch(query);
            }
            else
            {
                genericQueryDispatcher.Dispatch(query);
            }
        }
    }
}
