using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Infrastructure.Storage.UserSettings
{
    public class CustomizedPluginConfig
    {
        public string ID { get; set; }

        public string Name { get; set; }

        public string Actionword { get; set; }

        public bool Disabled { get; set; }
    }
}
