using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.CMD
{
    public class CMDHistory
    {
        public bool ReplaceWinR { get; set; } = true;
        public bool LeaveCmdOpen { get; set; }
        public Dictionary<string, int> Count = new Dictionary<string, int>();

        public void AddCmdHistory(string cmdName)
        {
            if (Count.ContainsKey(cmdName))
            {
                Count[cmdName] += 1;
            }
            else
            {
                Count.Add(cmdName, 1);
            }
        }
    }
}
