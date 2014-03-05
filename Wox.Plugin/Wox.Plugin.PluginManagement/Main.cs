using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.PluginManagement
{
    public class Main:IPlugin
    {
        private PluginInitContext context;

        public List<Result> Query(Query query)
        {
            return null;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }
    }
}
