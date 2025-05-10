// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

public record TryCommandKeybindingMessage(bool Ctrl, bool Alt, bool Shift, bool Win, VirtualKey Key)
{
    public bool Handled { get; set; }
}
