// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CmdPal.Extensions;
using Microsoft.UI.Xaml.Controls;

namespace WindowsCommandPalette;

public sealed class ContextItemViewModel : INotifyPropertyChanged
{
    public ICommand Command { get; set; }

    internal string Name => Command.Name;

    internal IconData Icon => Command.Icon.Dark;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal bool CanInvoke => Command != null && Command is IInvokableCommand or IPage;

    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(Icon.Icon);

    public ContextItemViewModel(ICommand cmd)
    {
        this.Command = cmd;
        this.Command.PropChanged += Action_PropertyChanged;
    }

    public ContextItemViewModel(ICommandContextItem model)
    {
        this.Command = model.Command;
        this.Command.PropChanged += Action_PropertyChanged;
    }

    private void Action_PropertyChanged(object sender, Microsoft.CmdPal.Extensions.PropChangedEventArgs args)
    {
        this.PropertyChanged?.Invoke(this, new(args.PropertyName));
    }
}
