// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class ParseVSCodeAuthority
    {
        private static readonly Dictionary<string, WorkspaceEnvironment> EnvironmentTypes = new()
        {
            { string.Empty, WorkspaceEnvironment.Local },
            { "ssh-remote", WorkspaceEnvironment.RemoteSSH },
            { "wsl", WorkspaceEnvironment.RemoteWSL },
            { "vsonline", WorkspaceEnvironment.Codespaces },
            { "dev-container", WorkspaceEnvironment.DevContainer },
            { "tunnel", WorkspaceEnvironment.RemoteTunnel },
        };

        private static string GetRemoteName(string authority)
        {
            if (authority is null)
            {
                return null;
            }

            var pos = authority.IndexOf('+');
            if (pos < 0)
            {
                return authority;
            }

            return authority[..pos];
        }

        public static (WorkspaceEnvironment? WorkspaceEnvironment, string MachineName) GetWorkspaceEnvironment(string authority)
        {
            var remoteName = GetRemoteName(authority);
            var machineName = remoteName.Length < authority.Length ? authority[(remoteName.Length + 1)..] : null;
            return EnvironmentTypes.TryGetValue(remoteName, out WorkspaceEnvironment workspace) ?
                (workspace, machineName) :
                (null, null);
        }
    }
}
