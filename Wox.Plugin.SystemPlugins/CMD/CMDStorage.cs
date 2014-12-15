using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.SystemPlugins.CMD
{
    public class CMDStorage : JsonStrorage<CMDStorage>
    {
        [JsonProperty]
        public Dictionary<string, int> CMDHistory = new Dictionary<string, int>();

        protected override string ConfigName
        {
            get { return "CMDHistory"; }
        }

        public void AddCmdHistory(string cmdName)
        {
            if (CMDHistory.ContainsKey(cmdName))
            {
                CMDHistory[cmdName] += 1;
            }
            else
            {
                CMDHistory.Add(cmdName, 1);
            }
            Save();
        }
    }
}
