using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.Program
{
    public class ProgramStorage : JsonStrorage<ProgramStorage>
    {
        [JsonProperty]
        public List<ProgramSource> ProgramSources { get; set; }

        [JsonProperty]
        public string ProgramSuffixes { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(true)]
        public bool EnableStartMenuSource { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(true)]
        public bool EnableRegistrySource { get; set; }

        protected override string ConfigFolder
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        protected override ProgramStorage LoadDefault()
        {
            ProgramSources = new List<ProgramSource>();
            EnableStartMenuSource = true;
            EnableRegistrySource = true;
            return this;
        }

        protected override void OnAfterLoad(ProgramStorage storage)
        {
            if (string.IsNullOrEmpty(storage.ProgramSuffixes))
            {
                storage.ProgramSuffixes = "lnk;exe;appref-ms;bat";
            }
        }

        protected override string ConfigName
        {
            get { return "setting"; }
        }
    }
}
