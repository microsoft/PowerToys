using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public interface IPowerToySettings
    {
        string name { get; set; }
        string version { get; set; }
        string IPCOutMessage();
    }
}
