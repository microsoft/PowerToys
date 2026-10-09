// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

/// <summary>
/// Service to load favicons for websites.
/// </summary>
public interface IFaviconLoader
{
    Task<IRandomAccessStream?> TryGetFaviconAsync(Uri siteUri, CancellationToken ct = default);
}
