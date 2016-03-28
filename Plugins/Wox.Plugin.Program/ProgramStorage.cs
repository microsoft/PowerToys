using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.Program
{
    [Serializable]
    public class ProgramStorage : JsonStrorage<ProgramStorage>
    {
        [JsonProperty]
        public List<ProgramSource> ProgramSources { get; set; }


        [JsonProperty]
        public string[] ProgramSuffixes { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(true)]
        public bool EnableStartMenuSource { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(true)]
        public bool EnableRegistrySource { get; set; }

        protected override ProgramStorage LoadDefault()
        {
            ProgramSources = new List<ProgramSource>();
            EnableStartMenuSource = true;
            EnableRegistrySource = true;
            return this;
        }

        protected override void OnAfterLoad(ProgramStorage storage)
        {
            if (storage.ProgramSuffixes == null || storage.ProgramSuffixes.Length == 0)
            {
                storage.ProgramSuffixes = new[] {"bat", "appref-ms", "exe", "lnk"};
            }
        }

        protected override string FileName { get; } = "settings_plugin_program";
    }
}
