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
    /// Gets or sets the npm registry URL to install from. When null or empty, the default
    /// registry configured on the machine is used.
    /// </summary>
    public string? Registry { get; set; }
}
