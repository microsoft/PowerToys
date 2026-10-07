// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

/// <summary>Holds one application's manifest data and logical logo references before catalog capture.</summary>
internal sealed class PackagedAppMetadata
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string AppUserModelId { get; set; } = string.Empty;

    /// <summary>Gets or sets the manifest-declared executable used as search metadata, not as an activation target.</summary>
    public string Executable { get; set; } = string.Empty;

    /// <summary>Gets or sets execution alias filenames declared by this application.</summary>
    public IReadOnlyList<string> ExecutionAliases { get; set; } = [];

    /// <summary>Gets or sets recognized Edge PWA launch metadata used to associate equivalent packages.</summary>
    public EdgePwaLaunchInfo? EdgePwaLaunch { get; set; }

    /// <summary>Gets or sets the manifest's small-logo reference, with asset selection deferred to rendering.</summary>
    public string SmallLogoUri { get; set; } = string.Empty;

    /// <summary>Gets or sets the manifest's large-logo fallback reference for the details icon.</summary>
    public string LargeLogoUri { get; set; } = string.Empty;

    public bool CanRunElevated { get; set; }

    public required PackageMetadata Package { get; set; }
}
