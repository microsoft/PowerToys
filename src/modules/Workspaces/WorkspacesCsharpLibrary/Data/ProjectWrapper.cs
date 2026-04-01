// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace WorkspacesCsharpLibrary.Data;

public struct ProjectWrapper
{
    public string Id { get; set; }

    public string Name { get; set; }

    public long CreationTime { get; set; }

    public long LastLaunchedTime { get; set; }

    public bool IsShortcutNeeded { get; set; }

    public bool MoveExistingWindows { get; set; }

    public List<MonitorConfigurationWrapper> MonitorConfiguration { get; set; }

    public List<ApplicationWrapper> Applications { get; set; }
}
