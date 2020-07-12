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
        public List<dynamic> workspaces3 { get; set; }
    }
}
