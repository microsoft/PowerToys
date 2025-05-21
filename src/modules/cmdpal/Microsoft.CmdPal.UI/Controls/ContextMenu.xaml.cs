// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ContextMenu : UserControl
{
    public static readonly DependencyProperty ContextMenuStackViewModelProperty =
        DependencyProperty.Register(nameof(ContextMenuStackViewModel), typeof(PageViewModel), typeof(CommandBar), new PropertyMetadata(null));

    public ContextMenuStackViewModel ContextMenuStackViewModel
    {
        get => (ContextMenuStackViewModel)GetValue(ContextMenuStackViewModelProperty);
        set => SetValue(ContextMenuStackViewModelProperty, value);
    }

    public ContextMenu()
    {
        this.InitializeComponent();
    }
}
