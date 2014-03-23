using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.System.ProgramSources
{
    [global::System.ComponentModel.Browsable(false)]
    public class UserStartMenuProgramSource : FileSystemProgramSource
    {
        public UserStartMenuProgramSource()
            : base(Environment.GetFolderPath(Environment.SpecialFolder.Programs))
        {
        }

        public UserStartMenuProgramSource(ProgramSource source)
            : this()
        {
            this.BonusPoints = source.BonusPoints;
        }

        public override string ToString()
        {
            return typeof(UserStartMenuProgramSource).Name;
        }
    }
}
