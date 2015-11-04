using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.CMD
{
    public class CMDStorage : JsonStrorage<CMDStorage>
    {
        [JsonProperty]
        public bool ReplaceWinR { get; set; }

        [JsonProperty]
        public bool LeaveCmdOpen { get; set; }

        [JsonProperty]
        public Dictionary<string, int> CMDHistory = new Dictionary<string, int>();

        protected override string ConfigFolder
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        protected override string ConfigName
        {
            get { return "CMDHistory"; }
        }

        protected override CMDStorage LoadDefault()
        {
            ReplaceWinR = true;
            return this;
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
