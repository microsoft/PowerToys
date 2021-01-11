using Microsoft.PowerToys.Run.Plugin.VSCodeWorkspaces.VSCodeHelper;

namespace Microsoft.PowerToys.Run.Plugin.VSCodeWorkspaces.RemoteMachinesHelper
{
    public class VSCodeRemoteMachine
    {
        public string Host { get; set; }

        public string User { get; set; }

        public string HostName { get; set; }

        public VSCodeInstance VSCodeInstance { get; set; }
    }
}
