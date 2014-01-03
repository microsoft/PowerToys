using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WinAlfred.Helper;
using WinAlfred.Plugin;

namespace WinAlfred.Commands
{
    public class CommandDispatcher
    {
        private PluginCommand pluginCmd = new PluginCommand();
        private SystemCommand systemCmd = new SystemCommand();

        //public delegate void resultUpdateDelegate(List<Result> results);

        //public event resultUpdateDelegate OnResultUpdateEvent;

        //protected virtual void OnOnResultUpdateEvent(List<Result> list)
        //{
        //    resultUpdateDelegate handler = OnResultUpdateEvent;
        //    if (handler != null) handler(list);
        //}

        public void DispatchCommand(Query query)
        {
            systemCmd.Dispatch(query);
            pluginCmd.Dispatch(query);
        }
    }
}
