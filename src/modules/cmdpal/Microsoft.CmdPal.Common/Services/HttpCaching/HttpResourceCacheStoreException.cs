// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Services.HttpCaching;

/// <summary>Identifies a filesystem failure while caching an HTTP resource.</summary>
public sealed class HttpResourceCacheStoreException : HttpRequestException
{
    internal HttpResourceCacheStoreException(string path, Exception innerException)
        : base($"Could not access HTTP resource cache at '{path}'.", innerException)
    {
    }
}
