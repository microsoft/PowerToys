// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Represents the exactly-one application payload carried by a catalog item.
/// </summary>
/// <remarks>
/// Payload implementations own their materialization and structural equality semantics.
/// The polymorphic metadata is part of the catalog cache schema.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Win32AppPayload), "win32")]
[JsonDerivedType(typeof(PackagedAppSnapshot), "packaged")]
internal interface IAppCatalogPayload
{
    /// <summary>Materializes the consumer-facing application.</summary>
    AppItem ToAppItem();

    /// <summary>Gets the persisted command ID of this source representation.</summary>
    string GetCommandId();

    /// <summary>
    /// Gets a stable cross-source identity when this payload can identify its canonical application.
    /// </summary>
    /// <returns>The canonical identity hint, or <see langword="null"/> when none is known.</returns>
    string? GetCanonicalIdentityHint();

    /// <summary>
    /// Gets the executable target when this payload has no launch-specific arguments or working directory.
    /// </summary>
    /// <returns>The target path, or <see langword="null"/> when target association is not safe.</returns>
    string? GetCanonicalTargetPath();
}
