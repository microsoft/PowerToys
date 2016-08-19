using System;
using System.Collections.Generic;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    class StartMenu : Win32
    {
        public override List<Program> LoadPrograms()
        {
            var directory1 = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var directory2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            var programs = new List<Program>();
            GetAppFromDirectory(programs, directory1, MaxDepth);
            GetAppFromDirectory(programs, directory2, MaxDepth);
            return programs;
        }
    }
}
