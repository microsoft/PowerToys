// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Services.HttpCaching.Abstraction;

public sealed class CachedHttpFetchResult
{
    public CachedHttpResource Resource { get; }

    public bool UsedFallbackCache { get; }

    public CachedHttpFetchResult(CachedHttpResource resource, bool usedFallbackCache)
    {
        Resource = resource;
        UsedFallbackCache = usedFallbackCache;
    }
}
