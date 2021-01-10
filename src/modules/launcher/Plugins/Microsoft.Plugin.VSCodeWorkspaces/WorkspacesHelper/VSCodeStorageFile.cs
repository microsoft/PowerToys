using System.Collections.Generic;

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
