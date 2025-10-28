// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

internal interface IBookmarkResolver
{
    Task<(bool Success, Classification Result)> TryClassifyAsync(string input, CancellationToken cancellationToken = default);

    Classification ClassifyOrUnknown(string input);
}
