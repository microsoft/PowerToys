// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

/// <summary>
/// npm package details for a JavaScript/TypeScript ("jsonrpc") gallery extension. Describes the
/// package to install and the optional registry to use.
/// </summary>
public sealed class GalleryNpmPackage
{
    /// <summary>
    /// Gets or sets the npm package identifier (for example, "@publisher/cmdpal-my-extension").
    /// </summary>
    public string Package { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exact package version to install (for example, "1.4.2"). The installer
    /// rejects ranges and dist tags such as "latest" so the installed artifact matches the catalog.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Subresource Integrity value (for example, "sha512-...") for the approved
    /// package tarball. The installer checks npm's resolved package against this value before
    /// promoting it, so a registry that serves different bytes for the same version is rejected.
    /// </summary>
    public string Integrity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the npm registry URL to install from. When null or empty, npm uses the
    /// machine default. When present, it must be an absolute HTTPS URL on the approved allowlist.
    /// </summary>
    public string? Registry { get; set; }
}
