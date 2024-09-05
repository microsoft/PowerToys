// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Terminal.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.CmdPal.Extensions;
using Windows.ApplicationModel.AppExtensions;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace DeveloperCommandPalette;

public sealed class ContextItemViewModel : INotifyPropertyChanged
{
    internal ICommand Command;

    internal string Name => Command.Name;

    internal IconDataType Icon => Command.Icon;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal bool CanInvoke => Command != null && Command is IInvokableCommand or IPage;

    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(Icon.Icon);

    public ContextItemViewModel(ICommand action)
    {
        this.Command = action;
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
