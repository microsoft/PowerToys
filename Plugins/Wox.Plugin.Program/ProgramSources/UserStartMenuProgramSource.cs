using System;
using System.ComponentModel;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    [Browsable(false)]
    public class UserStartMenuProgramSource : FileSystemProgramSource
    {
        public UserStartMenuProgramSource()
            : base(Environment.GetFolderPath(Environment.SpecialFolder.Programs))
        {
        }

        public UserStartMenuProgramSource(ProgramSource source)
            : this()
        {
            BonusPoints = source.BonusPoints;
        }

        public override string ToString()
        {
            return typeof(UserStartMenuProgramSource).Name;
        }
    }
}
