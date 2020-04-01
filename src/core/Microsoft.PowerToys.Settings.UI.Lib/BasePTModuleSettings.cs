using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public abstract class BasePTModuleSettings : IPowerToySettings
    {
        public string name { get; set; }
        public string version { get; set; }

        public virtual string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        public string IPCOutMessage()
        {
            return "{\"powertoys\":{\"" + this.name + "\":" + this.ToJsonString() + "}}";
        }
    }
}
