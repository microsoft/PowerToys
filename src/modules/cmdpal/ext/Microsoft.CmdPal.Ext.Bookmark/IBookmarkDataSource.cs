// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public interface IBookmarkDataSource
{
    Task<string> GetBookmarkDataAsync();

    Task SaveBookmarkDataAsync(string jsonData);
}
