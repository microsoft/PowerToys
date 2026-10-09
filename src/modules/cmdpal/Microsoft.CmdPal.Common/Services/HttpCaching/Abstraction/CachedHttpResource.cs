// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Services.HttpCaching.Abstraction;

internal sealed class CachedHttpResource(string contentPath, string? contentType, bool fromCache, bool wasRevalidated)
{
    public string ContentPath { get; } = Path.GetFullPath(contentPath);

    public Uri ContentUri => new(ContentPath);

    public string? ContentType { get; } = contentType;

    public bool FromCache { get; } = fromCache;

    public bool WasRevalidated { get; } = wasRevalidated;
}
