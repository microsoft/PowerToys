using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Wox.Plugin.DotnetPluginTest
{
    public class Main : IPlugin
    {
        public List<Result> Query(Query query)
        {
            return new List<Result>();
        }

        public void Init(PluginInitContext context)
        {
           var s = JsonSerializer.Create();
        }
    }
}
