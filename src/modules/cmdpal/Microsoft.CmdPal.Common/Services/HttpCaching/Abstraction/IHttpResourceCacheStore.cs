// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Services.HttpCaching.Abstraction;

internal interface IHttpResourceCacheStore
{
    CachedHttpResourceEntry GetEntry(Uri resourceUri, string? fileNameHint = null);

    CachedHttpResource? TryGetFresh(CachedHttpResourceEntry entry, TimeSpan? timeToLiveOverride);

    CachedHttpResource? TryGetCached(CachedHttpResourceEntry entry, bool fromCache, bool wasRevalidated);

    CachedHttpResource? UpdateAfterNotModified(CachedHttpResourceEntry entry, HttpResponseMessage response);

    Task<CachedHttpResource> SaveResponseAsync(CachedHttpResourceEntry entry, HttpResponseMessage response, CancellationToken cancellationToken);

    void Prune(IEnumerable<Uri> retainedResourceUris);
}
