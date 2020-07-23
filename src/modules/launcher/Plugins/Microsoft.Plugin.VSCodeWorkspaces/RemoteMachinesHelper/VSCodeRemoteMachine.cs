using Microsoft.Plugin.VSCodeWorkspaces.VSCodeHelper;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Plugin.VSCodeWorkspaces.RemoteMachinesHelper
{
    public class VSCodeRemoteMachine
    {
        public string Host { get; set; }

        public string User { get; set; }

        public string HostName { get; set; }

        public VSCodeInstance VSCodeInstance { get; set; }
    }
}
