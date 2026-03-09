// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.RaycastStore.GitHub;

internal sealed class RaycastExtensionInfo
{
    public string Name { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string License { get; set; } = string.Empty;

    public List<RaycastCommand> Commands { get; set; } = new();

    public List<string> Categories { get; set; } = new();

    public List<string> Contributors { get; set; } = new();

    public string DirectoryName { get; set; } = string.Empty;

    public bool IsWindowsConfirmed { get; set; }

    public string IconUrl { get; set; } = string.Empty;
}
