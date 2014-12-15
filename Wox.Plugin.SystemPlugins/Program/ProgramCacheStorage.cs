using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.SystemPlugins.Program
{
    [Serializable]
    public class ProgramCacheStorage : BinaryStorage<ProgramCacheStorage>
    {
        public List<Program> Programs = new List<Program>();

        protected override string ConfigName
        {
            get { return "ProgramIndexCache"; }
        }
    }
}