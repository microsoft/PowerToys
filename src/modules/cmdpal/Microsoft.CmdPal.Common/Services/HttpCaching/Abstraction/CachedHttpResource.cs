// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Services.HttpCaching.Abstraction;

public sealed class CachedHttpResource
{
    public string ContentPath { get; }

    public Uri ContentUri => new(ContentPath);

    public string? ContentType { get; }

    public Uri? FinalUri { get; }

    public bool FromCache { get; }

    public bool WasRevalidated { get; }

    public CachedHttpResource(string contentPath, string? contentType, bool fromCache, bool wasRevalidated, Uri? finalUri = null)
    {
        ContentPath = Path.GetFullPath(contentPath);
        ContentType = contentType;
        FinalUri = finalUri;
        FromCache = fromCache;
        WasRevalidated = wasRevalidated;
    }
}
