// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace TopToolbar.Models;

public class ToolbarAction
{
    public ToolbarActionType Type { get; set; } = ToolbarActionType.CommandLine;

    public string Command { get; set; }

    public string Arguments { get; set; }

    public string WorkingDirectory { get; set; }

    public bool RunAsAdmin { get; set; }
}
