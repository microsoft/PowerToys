using System;

namespace Wox.Plugin.Program.ProgramSources
{

    [Serializable]
    public sealed class UserStartMenuProgramSource : FileSystemProgramSource
    {
        public UserStartMenuProgramSource()
        {
            Location = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        }
    }
}