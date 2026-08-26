// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Messages;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

public interface ICommandBarInteractionTarget
{
    void SetCommandContext(ICommandBarContext? context);

    void OpenContextMenu();

    void CloseContextMenu();

    bool TryCommandKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key);
}
