// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

/// <summary>
/// npm metadata for a JavaScript/TypeScript ("jsonrpc") gallery extension. Describes the
/// package to install and, optionally, the registry it should be pulled from.
/// </summary>
public sealed class GalleryNpmPackage
{
    /// <summary>
    /// Gets or sets the npm package identifier (for example, "@publisher/cmdpal-my-extension").
    /// </summary>
    public string Package { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exact package version to install (for example, "1.4.2"). The install
    /// flow requires an exact version; ranges and dist-tags (such as "latest") are rejected so
    /// the artifact that is installed always matches the one the catalog approved.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Subresource Integrity value (for example, "sha512-...") of the approved
    /// package tarball. The install flow verifies the resolved package against this value before
    /// promoting it, so a registry that serves different bytes for the same version is rejected.
    /// </summary>
    public string Integrity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the npm registry URL to install from. When null or empty, the default
    /// registry configured on the machine is used. When present it must be an absolute HTTPS URL
    /// on the approved allowlist.
    /// </summary>
    public string? Registry { get; set; }
}
