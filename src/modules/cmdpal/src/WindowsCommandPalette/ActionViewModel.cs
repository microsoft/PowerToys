// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.UI.Xaml.Controls;

namespace DeveloperCommandPalette;

public sealed class ActionViewModel(ICommand model)
{
    public ICommand Command => model;

    internal readonly string Name = model.Name;
    internal readonly string Icon = model.Icon.Icon;

    internal bool CanInvoke => model is IInvokableCommand;

    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(Icon);
}
