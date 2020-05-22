using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class VSCodeStorageFile
    {
        public openedPathsList openedPathsList { get; set; }
    }

    public class openedPathsList
    {
        public List<String> workspaces3 { get; set; }
    }
}
