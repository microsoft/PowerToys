using System.Collections.Generic;

namespace WinAlfred.Plugin
{
    public interface IPlugin
    {
        List<Result> Query(Query query);
        void Init();
    }
}