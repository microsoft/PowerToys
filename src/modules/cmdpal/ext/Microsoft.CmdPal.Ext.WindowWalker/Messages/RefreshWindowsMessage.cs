// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.WindowWalker.Messages;

/// <summary>
/// Message sent when the window list needs to be refreshed.
/// Used by CloseWindowCommand and EndTaskCommand to notify
/// WindowWalkerListPage to refresh after closing/killing a window.
/// </summary>
internal sealed class RefreshWindowsMessage
{
}
