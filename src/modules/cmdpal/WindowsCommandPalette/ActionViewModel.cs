// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.UI.Xaml.Controls;

namespace WindowsCommandPalette;

public sealed class ActionViewModel(ICommand cmd)
{
    public ICommand Command => cmd;

    internal bool CanInvoke => cmd is IInvokableCommand;

    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(Command.Icon.Dark.Icon);
}
