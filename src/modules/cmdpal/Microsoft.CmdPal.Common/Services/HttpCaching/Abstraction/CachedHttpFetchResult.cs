// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Services.HttpCaching.Abstraction;

internal sealed class CachedHttpFetchResult(CachedHttpResource resource, bool usedFallbackCache)
{
    public CachedHttpResource Resource { get; } = resource;

    public bool UsedFallbackCache { get; } = usedFallbackCache;
}
