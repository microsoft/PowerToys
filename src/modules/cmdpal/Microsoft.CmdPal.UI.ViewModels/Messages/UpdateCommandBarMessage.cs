// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Updates the command bar for a selected item or content page.
/// </summary>
public record UpdateCommandBarMessage(ICommandBarContext? ViewModel)
{
}
