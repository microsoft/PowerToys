using System.Collections.Generic;

namespace Microsoft.Plugin.Shell
{
    public class Settings
    {
        public Shell Shell { get; set; } = Shell.RunCommand;
        
        // not overriding Win+R 
        // crutkas we need to earn the right for Win+R override
        public bool ReplaceWinR { get; set; } = false;
        public bool LeaveShellOpen { get; set; }
        public bool RunAsAdministrator { get; set; } = false;

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

    public enum Shell
    {
        Cmd = 0,
        Powershell = 1,
        RunCommand = 2,

    }
}
