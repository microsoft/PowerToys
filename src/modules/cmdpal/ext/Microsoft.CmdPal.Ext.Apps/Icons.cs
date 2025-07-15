// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

internal sealed class Icons
{
    internal static IconInfo AllAppsIcon => IconHelpers.FromRelativePath("Assets\\AllApps.svg");

    internal static IconInfo OtherUserIcon => new("\uE7EE"); // OtherUser icon

    internal static IconInfo AdminIcon => new("\uE7EF"); // Admin icon

    internal static IconInfo OpenPathIcon => new("\ue838"); // Folder Open icon

    internal static IconInfo CopyIcon => new("\ue8c8"); // Copy icon
}
