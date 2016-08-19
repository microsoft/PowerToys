using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    public abstract class ProgramSource
    {
        public abstract List<Program> LoadPrograms();
    }
}