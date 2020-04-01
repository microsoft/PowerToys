using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public abstract class BasePTModuleSettings : IPowerToySettings
    {
        public string name { get; set; }
        public string version { get; set; }

        public string IPCOutMessage()
        {
            return "{\"powertoys\":{\"" + this.name + "\":" + this.ToString() + "}}";
        }
    }
}
