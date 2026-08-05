// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Represents the versioned on-disk container for independently validated source snapshots.
/// </summary>
internal sealed class AppCatalogCacheFile
{
    /// <summary>Gets the schema version written by this build.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Gets or sets the serialized schema version.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Gets or sets the UI language associated with localized metadata.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Gets or sets the cached source snapshots.</summary>
    public List<AppCatalogSourceSnapshot> Sources { get; set; } = [];

    /// <summary>Determines whether global schema and language state allow per-source validation.</summary>
    public bool IsCompatible(AppCatalogCacheContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return SchemaVersion == CurrentSchemaVersion
            && string.Equals(Language, context.Language, StringComparison.OrdinalIgnoreCase);
    }
}
