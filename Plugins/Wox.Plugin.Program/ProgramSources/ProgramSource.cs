using System;
using System.Collections.Generic;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    public abstract class ProgramSource
    {
        public abstract List<Program> LoadPrograms();
    }
}