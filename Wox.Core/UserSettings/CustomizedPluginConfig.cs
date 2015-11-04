using System;
using System.Collections.Generic;

namespace Wox.Core.UserSettings
{
    [Serializable]
    public class CustomizedPluginConfig
    {
        public string ID { get; set; }

        public string Name { get; set; }

        public string[] ActionKeywords { get; set; }

        public bool Disabled { get; set; }
    }
}
