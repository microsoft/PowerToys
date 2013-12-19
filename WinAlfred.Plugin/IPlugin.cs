using System.Collections.Generic;

namespace WinAlfred.Plugin
{
    public interface IPlugin
    {
        string GetActionName();
        List<Result> Query(Query query);
        void Init();
    }
}