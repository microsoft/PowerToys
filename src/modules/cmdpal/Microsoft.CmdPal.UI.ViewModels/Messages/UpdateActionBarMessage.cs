// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Used to update the action bar at the bottom to reflect the commands for a list item
/// </summary>
public record UpdateActionBarMessage(ListItemViewModel? ViewModel)
{
}
