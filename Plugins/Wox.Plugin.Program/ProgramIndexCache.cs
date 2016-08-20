using System;
using System.Collections.Generic;
using Wox.Plugin.Program.Programs;

namespace Wox.Plugin.Program
{
    [Serializable]
    public class ProgramIndexCache
    {
        public List<Win32> Programs = new List<Win32>();
    }
}