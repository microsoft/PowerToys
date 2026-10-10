// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Cache;

/// <summary>
/// Stores one source's independently aged and validated raw catalog snapshot.
/// </summary>
internal sealed class AppCatalogSourceSnapshot
{
    /// <summary>Gets or sets the stable source identity.</summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the opaque validation key captured for this snapshot.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>Gets or sets when this source snapshot was last authoritatively validated.</summary>
    public DateTimeOffset ValidatedAtUtc { get; set; }

    /// <summary>Gets or sets the raw source items, before catalog policy or visibility projection.</summary>
    public List<AppCatalogItem> Items { get; set; } = [];
}
