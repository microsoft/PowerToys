using System;

namespace Microsoft.Plugin.VSCodeWorkspaces.VSCodeHelper
{
    public enum VSCodeVersion
    {
        Stable = 1,
        Insiders = 2,
        Exploration = 3
    }

    public class VSCodeInstance
    {
        public VSCodeVersion VSCodeVersion { get; set; }

        public string ExecutablePath { get; set; } = String.Empty;

        public string AppData { get; set; } = String.Empty;
    }
}
