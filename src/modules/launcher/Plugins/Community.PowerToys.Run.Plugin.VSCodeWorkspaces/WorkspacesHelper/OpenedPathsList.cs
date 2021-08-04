// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class OpenedPathsList
    {
        public List<dynamic> Workspaces3 { get; set; }

        public List<VSCodeWorkspaceEntry> Entries { get; set; }
    }
}
