// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Apps;

/// <summary>
/// Contains the catalog's consumer-facing launch, display, search, command, and deferred-icon metadata.
/// </summary>
public sealed class AppItem
{
    /// <summary>Gets or sets the stable canonical catalog identity, separate from persisted command IDs.</summary>
    public string CatalogId { get; set; } = string.Empty;

    /// <summary>Gets or sets persisted command IDs retained from equivalent source representations.</summary>
    public IReadOnlyList<string> CommandIds { get; set; } = [];

    public string Name { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string IcoPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the launch path, which may be a shortcut or an app execution alias.</summary>
    public string ExePath { get; set; } = string.Empty;

    /// <summary>Gets or sets arguments identifying the discovered launch entry.</summary>
    /// <remarks>Launching through a shortcut already applies its arguments.</remarks>
    public string Arguments { get; set; } = string.Empty;

    public string DirPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the packaged or explicit desktop Windows application ID, separate from command identity.</summary>
    public string UserModelId { get; set; } = string.Empty;

    public bool IsPackaged { get; set; }

    public List<IContextItem>? Commands { get; set; }

    public string AppIdentifier { get; set; } = string.Empty;

    public string? PackageFamilyName { get; set; }

    /// <summary>Gets or sets the application target path, resolving execution aliases when available.</summary>
    public string? FullExecutablePath { get; set; }

    public string? JumboIconPath { get; set; }

    /// <summary>
    /// Gets or sets source aliases and other retained metadata that may participate in search and catalog policy.
    /// </summary>
    public IReadOnlyList<string> MatchTerms { get; set; } = [];

    public AppItem()
    {
    }
}
