using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using Mages.Core;

namespace Wox.Plugin.WindowWalker
{
    public class Main : IPlugin, IPluginI18n
    {
        private PluginInitContext Context { get; set; }

        static Main()
        {
            
        }

        public List<Result> Query(Query query)
        {
            return new List<Result>{ new Result()
            {
                Title = "Yo!"
            } };
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
        }

        public string GetTranslatedPluginTitle()
        {
            return Context.API.GetTranslation("wox_plugin_caculator_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return Context.API.GetTranslation("wox_plugin_caculator_plugin_description");
        }
    }
}
