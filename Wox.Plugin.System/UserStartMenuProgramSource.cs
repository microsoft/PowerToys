using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.System
{
    [global::System.ComponentModel.Browsable(false)]
    public class UserStartMenuProgramSource : FileSystemProgramSource
    {
        public UserStartMenuProgramSource()
            : base(Environment.GetFolderPath(Environment.SpecialFolder.Programs))
        {
        }

        public UserStartMenuProgramSource(Wox.Infrastructure.UserSettings.ProgramSource source)
            : this()
        {
            this.BonusPoints = source.BounsPoints;
        }
    }
}
