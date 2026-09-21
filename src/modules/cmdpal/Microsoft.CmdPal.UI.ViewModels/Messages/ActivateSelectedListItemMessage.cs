// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Used to perform a list item's command when the user presses enter in the search box.
/// Recipients set <see cref="Handled"/> when they actually invoked a command so the shell
/// does not also queue the key.
/// </summary>
public record ActivateSelectedListItemMessage
{
    public bool Handled { get; set; }
}
