using System;
using System.Collections.Generic;
using System.IO;

namespace Wox.Plugin.Program.Programs
{
    [Serializable]
    public class UnregisteredPrograms : Win32
    {
        public static List<Win32> All(List<Settings.ProgramSource> sources, string[] suffixes)
        {
            List<Win32> programs = new List<Win32>();
            foreach (var source in sources)
            {
                if (System.IO.Directory.Exists(source.Location) && source.MaxDepth >= -1)
                {
                    GetAppFromDirectory(programs, source.Location, source.MaxDepth, suffixes);
                }
            }
            return programs;
        }
    }
}