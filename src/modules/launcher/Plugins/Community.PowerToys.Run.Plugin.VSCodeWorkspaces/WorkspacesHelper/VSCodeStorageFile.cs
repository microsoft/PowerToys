using System.Collections.Generic;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class VSCodeStorageFile
    {
        public openedPathsList openedPathsList { get; set; }
    }

    public class VSCodeWorkspaceEntry
    {
        public string folderUri { get; set; }
        public string label { get; set; }
    }

    public class openedPathsList
    {
        public List<dynamic> workspaces3 { get; set; }

        public List<VSCodeWorkspaceEntry> entries { get; set; }
    }
}
