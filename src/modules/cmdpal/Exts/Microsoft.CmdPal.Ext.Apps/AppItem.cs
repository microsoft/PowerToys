// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

internal sealed class AppItem
{
    public string Name { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string IcoPath { get; set; } = string.Empty;

    public string ExePath { get; set; } = string.Empty;

    public string DirPath { get; set; } = string.Empty;

    public string UserModelId { get; set; } = string.Empty;

    public bool IsPackaged { get; set; }

    public List<CommandContextItem>? Commands { get; set; }

    public AppItem()
    {
    }
}
