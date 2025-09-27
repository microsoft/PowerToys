// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

public interface IBookmarkIconLocator
{
    Task<IIconInfo> GetIconForPath(Classification classification, CancellationToken cancellationToken = default);
}
