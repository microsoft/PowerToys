// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class CommandBar : UserControl, ICurrentPageAware, ICommandBarInteractionTarget
{
    private long _commandContextVersion;

    public CommandBarViewModel ViewModel { get; } = new();

    public event EventHandler? FocusSearchRequested;

    public PageViewModel? CurrentPageViewModel
    {
        get => (PageViewModel?)GetValue(CurrentPageViewModelProperty);
        set => SetValue(CurrentPageViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for CurrentPage.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CurrentPageViewModelProperty =
        DependencyProperty.Register(nameof(CurrentPageViewModel), typeof(PageViewModel), typeof(CommandBar), new PropertyMetadata(null));

    public CommandBar()
    {
        this.InitializeComponent();
        ContextControl.CloseRequested += (_, _) => CloseContextMenu();
        ContextControl.FocusSearchRequested += (_, _) => FocusSearchRequested?.Invoke(this, EventArgs.Empty);
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

        ViewModel.QueueSelectedItem(context);
        ContextControl.SetCommandContext(context);
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

        if (element is null)
        {
            // This is invoked from the "More" button on the command bar
            if (!(ContextControl.ViewModel.SelectedItem?.CanOpenContextMenu ?? false))
            {
                return;
            }

            ContextControl.PrepareForOpen(filterLocation);

            _ = DispatcherQueue.TryEnqueue(
                () =>
                {
                    ContextMenuFlyout.ShowAt(
                        MoreCommandsButton,
                        new FlyoutShowOptions()
                        {
                            ShowMode = FlyoutShowMode.Standard,
                            Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                        });
                });
        }
        else
        {
            // This is invoked from a specific element
            if (!(ContextControl.ViewModel.SelectedItem?.CanOpenContextMenu ?? false))
            {
                return;
            }

            ContextControl.PrepareForOpen(filterLocation);

            _ = DispatcherQueue.TryEnqueue(
            () =>
            {
                ContextMenuFlyout.ShowAt(
                    element,
                    new FlyoutShowOptions()
                    {
                        ShowMode = FlyoutShowMode.Standard,
                        Placement = placement ?? FlyoutPlacementMode.BottomEdgeAlignedLeft,
                        Position = position,
                    });
            });
        }
    }

    public void CloseContextMenu()
    {
        if (ContextMenuFlyout.IsOpen)
        {
            ContextMenuFlyout.Hide();
        }
    }

    public bool TryCommandKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        if (!(ContextControl.ViewModel.SelectedItem?.CanOpenContextMenu ?? false))
        {
            return false;
        }

        var result = ViewModel.CheckKeybinding(ctrl, alt, shift, win, key);

        if (result == ContextKeybindingResult.Hide)
        {
            CloseContextMenu();
            return true;
        }

        if (result == ContextKeybindingResult.KeepOpen)
        {
            OpenContextMenu();
            return true;
        }

        return false;
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

    /// <summary>
    /// Sets focus to the "More" button after closing the context menu,
    /// keeping keyboard navigation intuitive.
    /// </summary>
    public void FocusMoreCommandsButton()
    {
        MoreCommandsButton?.Focus(FocusState.Programmatic);
    }

    private void ContextMenuFlyout_Opened(object sender, object e)
    {
        // Focus the filter box so the flyout captures keyboard input,
        // then fire a single consolidated Narrator announcement.
        ContextControl.FocusSearchBox();
        ContextControl.AnnounceOpened();
    }
}
