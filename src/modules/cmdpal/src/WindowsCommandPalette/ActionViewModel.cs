// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.CommandPalette.Extensions;
using Windows.ApplicationModel.AppExtensions;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Collections.ObjectModel;
using Microsoft.Terminal.UI;

namespace DeveloperCommandPalette;

public sealed class ActionViewModel(ICommand model)
{
    public ICommand Command => model;
    internal readonly string Name = model.Name;
    internal readonly string Icon = model.Icon.Icon;

    internal bool CanInvoke => model is IInvokableCommand;
    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(Icon);
}
