using System.Collections.Generic;

namespace Wox.Plugin
{
    public interface IPlugin
    {
        List<Result> Query(Query query);
        void Init(PluginInitContext context);
    }
}