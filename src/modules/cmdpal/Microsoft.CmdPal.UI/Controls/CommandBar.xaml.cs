// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class CommandBar : UserControl,
    ICurrentPageAware
{
    public static readonly DependencyProperty CommandContextProperty =
        DependencyProperty.Register(nameof(CommandContext), typeof(ICommandBarContext), typeof(CommandBar), new PropertyMetadata(null, CommandContextChanged));

    public static readonly DependencyProperty CurrentPageViewModelProperty =
        DependencyProperty.Register(nameof(CurrentPageViewModel), typeof(PageViewModel), typeof(CommandBar), new PropertyMetadata(null));

    public CommandBarViewModel ViewModel { get; } = new();

    public PageViewModel? CurrentPageViewModel
    {
        get => (PageViewModel?)GetValue(CurrentPageViewModelProperty);
        set => SetValue(CurrentPageViewModelProperty, value);
    }

    public ICommandBarContext? CommandContext
    {
        get => (ICommandBarContext?)GetValue(CommandContextProperty);
        set => SetValue(CommandContextProperty, value);
    }

    public CommandBar()
    {
        this.InitializeComponent();
        Loaded += CommandBar_Loaded;
        Unloaded += CommandBar_Unloaded;
    }

    private static void CommandContextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var bar = (CommandBar)sender;
        if (bar.IsLoaded)
        {
            bar.ViewModel.SetContext((ICommandBarContext?)e.NewValue);
        }
    }

    private void CommandBar_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.SetContext(CommandContext);
    }

    private void CommandBar_Unloaded(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            return;
        }

        ViewModel.ClearContext();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS has a tendency to delete XAML bound methods over-aggressively")]
    private void PrimaryButton_Clicked(object sender, RoutedEventArgs e)
    {
        ViewModel.InvokePrimaryCommand();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS has a tendency to delete XAML bound methods over-aggressively")]
    private void SecondaryButton_Clicked(object sender, RoutedEventArgs e)
    {
        ViewModel.InvokeSecondaryCommand();
    }

    private void SettingsIcon_Clicked(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new OpenSettingsMessage());
    }

    private void MoreCommandsButton_Clicked(object sender, RoutedEventArgs e)
    {
        if (CommandContext is { } context)
        {
            WeakReferenceMessenger.Default.Send(
                new OpenContextMenuMessage(
                    new ContextMenuRequest(context)
                    {
                        Anchor = new ContextMenuAnchor(
                            MoreCommandsButton,
                            null,
                            FlyoutPlacementMode.TopEdgeAlignedRight,
                            ContextMenuFilterLocation.Bottom),
                    }));
        }
    }
}
