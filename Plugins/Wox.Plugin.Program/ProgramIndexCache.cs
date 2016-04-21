using System;
using System.Collections.Generic;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.Program
{
    [Serializable]
    public class ProgramIndexCache
    {
        public List<Program> Programs = new List<Program>();
    }
}