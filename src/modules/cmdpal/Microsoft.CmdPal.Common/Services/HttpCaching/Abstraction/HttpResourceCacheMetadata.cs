// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Services.HttpCaching.Abstraction;

internal sealed class HttpResourceCacheMetadata
{
    public string? ContentType { get; set; }

    public string? ETag { get; set; }

    public DateTimeOffset? ExpiresUtc { get; set; }

    public string FileName { get; set; } = "payload.bin";

    public DateTimeOffset? LastModifiedUtc { get; set; }

    public DateTimeOffset LastValidatedUtc { get; set; }

    public string SourceUri { get; set; } = string.Empty;
}
