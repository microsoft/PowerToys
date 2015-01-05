using System;

namespace Wox.Core.UserSettings
{
    [Serializable]
    public class CustomizedPluginConfig
    {
        public string ID { get; set; }

        public string Name { get; set; }

        public string Actionword { get; set; }

        public bool Disabled { get; set; }
    }
}
