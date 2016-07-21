using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    public sealed class CommonStartMenuProgramSource : FileSystemProgramSource
    {
        private const int CSIDL_COMMON_PROGRAMS = 0x17;
        
        // todo happlebao how to pass location before loadPrograms
        public CommonStartMenuProgramSource()
        {
            Location = getPath();
        }

        [DllImport("shell32.dll")]
        private static extern bool SHGetSpecialFolderPath(IntPtr hwndOwner, [Out] StringBuilder lpszPath, int nFolder,
            bool fCreate);

        private static string getPath()
        {
            var commonStartMenuPath = new StringBuilder(560);
            SHGetSpecialFolderPath(IntPtr.Zero, commonStartMenuPath, CSIDL_COMMON_PROGRAMS, false);

            return commonStartMenuPath.ToString();
        }
    }
}