using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class VSCodeWorkspace
    {
        public string Path { get; set; }

        public string RelativePath { get; set; }

        public string FolderName { get; set; }

        public string ExtraInfo { get; set; }

        public TypeWorkspace TypeWorkspace { get; set; }

        public VSCodeVersion VSCodeVersion { get; set; }

        public string VSCodeExecutable { get; set; }
    }

    public enum TypeWorkspace
    {
        Local = 1,
        Codespaces = 2,
        RemoteWSL = 3,
        RemoteSSH = 4,
        RemoteContainers = 5
    }

    public enum VSCodeVersion
    {
        Stable = 1,
        Insiders = 2,
        Exploration = 3
    }

}
