// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Invokes the keyboard-selected hover action on the current list row, if any.
/// </summary>
public class TryActivateHoverActionMessage
{
    public bool Handled { get; set; }
}
