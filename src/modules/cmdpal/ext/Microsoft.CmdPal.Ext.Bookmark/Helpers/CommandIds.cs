// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

internal static class CommandIds
{
    /// <summary>
    /// Returns id of a command associated with a bookmark item. This id is for a command that launches the bookmark - regardless of whether
    /// the bookmark type of if it is a placeholder bookmark or not.
    /// </summary>
    /// <param name="id">Bookmark ID</param>
    public static string GetLaunchBookmarkItemId(Guid id) => "Bookmarks.Launch." + id;
}
