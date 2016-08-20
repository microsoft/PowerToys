using System;
using System.Collections.Generic;

namespace Wox.Plugin.Program.Programs
{
    [Serializable]
    class StartMenu : Win32
    {
        public static IEnumerable<Win32> All(string[] suffixes)
        {
            var directory1 = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var directory2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            var programs = new List<Win32>();
            GetAppFromDirectory(programs, directory1, -1, suffixes);
            GetAppFromDirectory(programs, directory2, -1, suffixes);
            return programs;
        }
    }
}
