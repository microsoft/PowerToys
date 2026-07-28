// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Services.HttpCaching.Abstraction;

internal sealed class CachedHttpResourceEntry(
    Uri resourceUri,
    string entryDirectory,
    string metadataPath,
    string payloadPath,
    string payloadFileName,
    HttpResourceCacheMetadata? metadata)
{
    public Uri ResourceUri { get; } = resourceUri;

    public string EntryDirectory { get; } = Path.GetFullPath(entryDirectory);

    public string MetadataPath { get; } = Path.GetFullPath(metadataPath);

    public string PayloadPath { get; } = Path.GetFullPath(payloadPath);

    public string PayloadFileName { get; } = payloadFileName;

    public HttpResourceCacheMetadata? Metadata { get; } = metadata;
}
