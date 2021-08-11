// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.Properties;
using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.VSCodeHelper;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class VSCodeWorkspace
    {
        public string Path { get; set; }

        public string RelativePath { get; set; }

        public string FolderName { get; set; }

        public string ExtraInfo { get; set; }

        public TypeWorkspace TypeWorkspace { get; set; }

        public VSCodeInstance VSCodeInstance { get; set; }

        public string WorkspaceTypeToString()
        {
            switch (TypeWorkspace)
            {
                case TypeWorkspace.Local: return Resources.TypeWorkspaceLocal;
                case TypeWorkspace.Codespaces: return "Codespaces";
                case TypeWorkspace.RemoteContainers: return Resources.TypeWorkspaceContainer;
                case TypeWorkspace.RemoteSSH: return "SSH";
                case TypeWorkspace.RemoteWSL: return "WSL";
            }

            return string.Empty;
        }
    }

    public enum TypeWorkspace
    {
        Local = 1,
        Codespaces = 2,
        RemoteWSL = 3,
        RemoteSSH = 4,
        RemoteContainers = 5,
    }
}
