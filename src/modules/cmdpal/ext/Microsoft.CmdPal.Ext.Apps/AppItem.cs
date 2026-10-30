// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Apps;

public sealed class AppItem
{
    /// <summary>Gets or sets the stable application identity, separate from persisted command IDs.</summary>
    public string CatalogId { get; set; } = string.Empty;

    /// <summary>Gets or sets command IDs retained from released app representations.</summary>
    public IReadOnlyList<string> CommandIds { get; set; } = [];

    public string Name { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string IcoPath { get; set; } = string.Empty;

    public string ExePath { get; set; } = string.Empty;

    public string DirPath { get; set; } = string.Empty;

    public string UserModelId { get; set; } = string.Empty;

    public bool IsPackaged { get; set; }

    public List<IContextItem>? Commands { get; set; }

    public string AppIdentifier { get; set; } = string.Empty;

    public string? PackageFamilyName { get; set; }

    public string? FullExecutablePath { get; set; }

    public string? JumboIconPath { get; set; }

    public AppItem()
    {
    }
}
