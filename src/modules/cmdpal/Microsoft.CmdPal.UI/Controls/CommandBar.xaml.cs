// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class CommandBar : UserControl, ICurrentPageAware, ICommandBarInteractionTarget
{
    private long _commandContextVersion;

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

    public void SetCommandContext(ICommandBarContext? context)
    {
        var version = Interlocked.Increment(ref _commandContextVersion);
        if (!DispatcherQueue.HasThreadAccess)
        {
            _ = DispatcherQueue.TryEnqueue(() => ApplyCommandContext(context, version));
            return;
        }

        ApplyCommandContext(context, version);
    }

    private void ApplyCommandContext(ICommandBarContext? context, long version)
    {
        if (version != Volatile.Read(ref _commandContextVersion))
        {
            return;
        }

        CommandContext = context;
    }

    public void OpenContextMenu() =>
        OpenContextMenu(null, null, null, null, ContextMenuFilterLocation.Bottom);

    public void OpenContextMenu(
        ICommandBarContext? context,
        FrameworkElement? element = null,
        FlyoutPlacementMode? placement = null,
        Point? position = null,
        ContextMenuFilterLocation filterLocation = ContextMenuFilterLocation.Bottom)
    {
        if (context is not null)
        {
            SetCommandContext(context);
        }

        var menuContext = context ?? CommandContext;
        if (menuContext?.CanOpenContextMenu != true)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(
            new OpenContextMenuMessage(
                new ContextMenuRequest(menuContext)
                {
                    Anchor = new ContextMenuAnchor(
                        element ?? MoreCommandsButton,
                        position,
                        placement ?? (element is null ? FlyoutPlacementMode.TopEdgeAlignedRight : FlyoutPlacementMode.BottomEdgeAlignedLeft),
                        filterLocation),
                }));
    }

    public void CloseContextMenu() => WeakReferenceMessenger.Default.Send<ClosePaletteContextMenuMessage>();

    public bool TryCommandKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        var context = CommandContext;
        if (context?.CanOpenContextMenu != true)
        {
            return false;
        }

        var chord = KeyChordHelpers.FromModifiers(ctrl, alt, shift, win, key, 0);
        if (context.FindKeybinding(chord) is not { } command)
        {
            return false;
        }

        if (command.HasSubmenu)
        {
            // Finish routing the key before the flyout takes focus.
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (ReferenceEquals(context, CommandContext) && context.AllCommands.Contains(command))
                {
                    WeakReferenceMessenger.Default.Send(
                        new OpenContextMenuMessage(new ContextMenuRequest(context) { InitialSubmenu = command }));
                }
            });
        }
        else
        {
            ViewModel.InvokeContextCommand(command);
        }

        return true;
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
        OpenContextMenu();
    }

    public void FocusMoreCommandsButton()
    {
        MoreCommandsButton?.Focus(FocusState.Programmatic);
    }
}
