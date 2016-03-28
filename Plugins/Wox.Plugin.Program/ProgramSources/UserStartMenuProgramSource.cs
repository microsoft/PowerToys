using System;
using System.ComponentModel;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    [Browsable(false)]
    public class UserStartMenuProgramSource : FileSystemProgramSource
    {
        public UserStartMenuProgramSource(string[] suffixes)
            : base(Environment.GetFolderPath(Environment.SpecialFolder.Programs), suffixes)
        {
        }

        public UserStartMenuProgramSource(ProgramSource source)
            : this(source.Suffixes)
        {
            BonusPoints = source.BonusPoints;
        }

        public override string ToString()
        {
            return typeof(UserStartMenuProgramSource).Name;
        }
    }
}
