// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal enum ManagedIconSourceFactoryCacheKind
{
    StringIcons,
    Thumbnails,
}

internal readonly record struct ManagedIconSourceFactoryDiagnostics(
    IReadOnlyList<ManagedIconSourceFactoryCacheDiagnostics> Caches);

internal readonly record struct ManagedIconSourceFactoryCacheDiagnostics(
    ManagedIconSourceFactoryCacheKind Kind,
    string Name,
    AdaptiveCacheStatistics CacheStatistics);
