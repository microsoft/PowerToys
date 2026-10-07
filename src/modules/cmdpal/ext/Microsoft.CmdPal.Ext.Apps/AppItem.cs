// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Apps;

/// <summary>
/// Contains the materialized application metadata used by catalog consumers.
/// </summary>
/// <remarks>
/// The catalog may reuse this projection between publications. Consumers should treat it as read-only;
/// discovery and cache data remain in the immutable catalog payloads.
/// </remarks>
public sealed class AppItem
{
    /// <summary>Gets or sets the display name of the preferred source representation.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the display description, or an empty string when unavailable.</summary>
    public string Subtitle { get; set; } = string.Empty;

    /// <summary>Gets or sets the localized application type label used in details.</summary>
    /// <remarks>This is presentation text, not an identity or activation discriminator.</remarks>
    public string AppTypeLabel { get; set; } = string.Empty;

    /// <summary>Gets or sets the canonical catalog identity used for deduplication and visibility.</summary>
    /// <remarks>An empty identity allows the command's legacy name-based ID fallback.</remarks>
    public string CatalogId { get; set; } = string.Empty;

    /// <summary>Gets or sets command aliases retained from source representations and previous catalog identities.</summary>
    /// <remarks>The primary command ID is generated separately from <see cref="CatalogId"/>.</remarks>
    public IReadOnlyList<string> CommandIds { get; set; } = [];

    /// <summary>Gets or sets the shell launch target for a Win32 entry.</summary>
    /// <remarks>
    /// Retains the shortcut path when present; otherwise contains the discovered path or URI.
    /// Packaged entries leave this empty and activate through <see cref="AppUserModelId"/>.
    /// </remarks>
    public string LaunchTarget { get; set; } = string.Empty;

    /// <summary>Gets or sets the resolved Win32 target, which may be an executable, document, folder, or URI.</summary>
    /// <remarks>
    /// Uses an app execution alias's resolved target when available. Packaged projections leave this null.
    /// </remarks>
    public string? ResolvedTarget { get; set; }

    /// <summary>Gets or sets the discovered launch arguments, or an empty string when absent.</summary>
    /// <remarks>
    /// Retained for diagnostics and executable-name ranking. Primary invocation does not append them to
    /// <see cref="LaunchTarget"/>; launching a shortcut applies its stored arguments.
    /// </remarks>
    public string LaunchArguments { get; set; } = string.Empty;

    /// <summary>Gets or sets the directory containing the discovered entry, or the package installation directory.</summary>
    /// <remarks>For a shortcut, this is its containing directory, not its configured working directory.</remarks>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>Gets or sets a packaged AUMID or explicit desktop AppUserModelID, or an empty string when absent.</summary>
    /// <remarks>A desktop application ID does not imply packaged activation; <see cref="IsPackaged"/> selects that behavior.</remarks>
    public string AppUserModelId { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the preferred payload uses packaged activation and presentation.</summary>
    public bool IsPackaged { get; set; }

    /// <summary>Gets or sets whether discovery recognized this entry as a browser-hosted web application.</summary>
    /// <remarks>This classification is independent of packaged activation.</remarks>
    public bool IsWebApp { get; set; }

    /// <summary>Gets or sets the preferred packaged payload's family name, or null for a Win32 payload.</summary>
    public string? PackageFamilyName { get; set; }

    /// <summary>Gets or sets the preferred row-icon source as an image path, native resource reference or icon protocol request.</summary>
    /// <remarks>
    /// May include a resource index and intentionally differ from the launch target. An empty source allows
    /// target and generic fallbacks. The list projection creates icon requests; the host loads their images.
    /// </remarks>
    public string IconSource { get; set; } = string.Empty;

    /// <summary>Gets or sets the preferred details hero-icon source.</summary>
    /// <remarks>Null or empty falls back to <see cref="IconSource"/> and then the available launch or target sources.</remarks>
    public string? JumboIconSource { get; set; }

    /// <summary>
    /// Gets or sets source names, aliases, and other retained metadata used by search and catalog policy.
    /// </summary>
    public IReadOnlyList<string> MatchTerms { get; set; } = [];

    /// <summary>Gets or sets fully qualified executable discovery paths retained through catalog provenance.</summary>
    /// <remarks>Preserves executable-name matching when another source representation supplies the preferred payload.</remarks>
    public IReadOnlyList<string> ExecutableSourcePaths { get; set; } = [];

    /// <summary>Gets or sets payload-provided context-menu entries, with null treated as an empty list.</summary>
    /// <remarks>These are additional actions; the list projection appends the visibility action separately.</remarks>
    public List<IContextItem>? Commands { get; set; }
}
