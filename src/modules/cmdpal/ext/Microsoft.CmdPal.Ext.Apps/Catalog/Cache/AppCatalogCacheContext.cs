// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Cache;

/// <summary>
/// Captures the environment state used to validate independently cached source snapshots.
/// </summary>
internal sealed class AppCatalogCacheContext
{
    /// <summary>Gets the UI language used when localized application metadata was materialized.</summary>
    public required string Language { get; init; }

    /// <summary>Gets the current opaque validation key for each configured source.</summary>
    public required IReadOnlyDictionary<string, string> SourceKeys { get; init; }

    /// <summary>Gets the current time used for deterministic maximum-age checks.</summary>
    public required DateTimeOffset NowUtc { get; init; }

    /// <summary>Creates validation state without exposing raw source keys in the persisted cache.</summary>
    public static AppCatalogCacheContext Create(IEnumerable<IAppSource> sources, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var sourceKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var sourceState = $"{source.Id}={source.CacheKey}";
            var sourceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceState)));
            if (!sourceKeys.TryAdd(source.Id, sourceKey))
            {
                throw new ArgumentException($"Application source ID '{source.Id}' is not unique.", nameof(sources));
            }
        }

        return new AppCatalogCacheContext
        {
            Language = CultureInfo.CurrentUICulture.Name,
            SourceKeys = sourceKeys,
            NowUtc = nowUtc,
        };
    }
}
