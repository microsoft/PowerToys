// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.CmdPal.Core.Common;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Commands;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using CoreVirtualKeyStates = Windows.UI.Core.CoreVirtualKeyStates;
using VirtualKey = Windows.System.VirtualKey;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ParameterRunTemplateSelector : DataTemplateSelector
{
    public DataTemplate? LabelRunTemplate { get; set; }

    public DataTemplate? StringParamTemplate { get; set; }

    public DataTemplate? ButtonParamTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        return item switch
        {
            LabelRunViewModel => LabelRunTemplate,
            StringParameterRunViewModel => StringParamTemplate,
            CommandParameterRunViewModel => ButtonParamTemplate,
            _ => base.SelectTemplateCore(item),
        };
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
