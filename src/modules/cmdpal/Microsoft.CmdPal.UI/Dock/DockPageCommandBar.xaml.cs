// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Point = Windows.Foundation.Point;
using VirtualKey = Windows.System.VirtualKey;

namespace Microsoft.CmdPal.UI.Dock;

public sealed partial class DockPageCommandBar : UserControl, ICommandBarInteractionTarget, IDisposable
{
    private long _commandContextVersion;

    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(nameof(CurrentPage), typeof(PageViewModel), typeof(DockPageCommandBar), new PropertyMetadata(null));

    public CommandBarViewModel ViewModel { get; } = new();

    public PageViewModel? CurrentPage
    {
        get => (PageViewModel?)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public event EventHandler? FocusSearchRequested;

    public DockPageCommandBar()
    {
        InitializeComponent();
        ContextControl.CloseRequested += ContextControl_CloseRequested;
        ContextControl.FocusSearchRequested += ContextControl_FocusSearchRequested;
    }

    internal bool HasOpenTransientUi => ContextMenuFlyout.IsOpen;

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

    internal void ShowContextMenu(
        ICommandBarContext context,
        FrameworkElement target,
        FlyoutPlacementMode placement,
        Point? position,
        ContextMenuFilterLocation filterLocation)
    {
        if (!context.CanOpenContextMenu)
        {
            return;
        }

        SetCommandContext(context);
        ContextControl.PrepareForOpen(filterLocation);
        PreparePopupForShow(ContextMenuFlyout, target);
        ContextMenuFlyout.ShowAt(
            target,
            new FlyoutShowOptions
            {
                ShowMode = FlyoutShowMode.Standard,
                Placement = placement,
                Position = position,
            });
    }

    public bool TryCommandKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        var result = ViewModel.CheckKeybinding(ctrl, alt, shift, win, key);
        if (result == ContextKeybindingResult.KeepOpen && ViewModel.SelectedItem is ICommandBarContext context)
        {
            ShowContextMenu(
                context,
                MoreCommandsButton,
                FlyoutPlacementMode.TopEdgeAlignedRight,
                null,
                ContextMenuFilterLocation.Bottom);
        }
        else if (result == ContextKeybindingResult.Hide)
        {
            CloseContextMenu();
        }

        return result != ContextKeybindingResult.Unhandled;
    }

    public void OpenContextMenu() => OpenSelectedItemContextMenu();

    internal void OpenSelectedItemContextMenu()
    {
        if (ViewModel.SelectedItem is ICommandBarContext context)
        {
            ShowContextMenu(
                context,
                MoreCommandsButton,
                FlyoutPlacementMode.TopEdgeAlignedRight,
                null,
                ContextMenuFilterLocation.Bottom);
        }
    }

    public void CloseContextMenu()
    {
        if (ContextMenuFlyout.IsOpen)
        {
            ContextMenuFlyout.Hide();
        }
    }

    private static void PreparePopupForShow(FlyoutBase popup, FrameworkElement placementTarget)
    {
        if (placementTarget.XamlRoot is not null && popup.XamlRoot != placementTarget.XamlRoot)
        {
            popup.XamlRoot = placementTarget.XamlRoot;
        }
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e) => ViewModel.InvokePrimaryCommand();

    private void SecondaryButton_Click(object sender, RoutedEventArgs e) => ViewModel.InvokeSecondaryCommand();

    private void MoreCommandsButton_Click(object sender, RoutedEventArgs e) => OpenSelectedItemContextMenu();

    private void ContextMenuFlyout_Opened(object sender, object e)
    {
        ContextControl.FocusSearchBox();
        ContextControl.AnnounceOpened();
    }

    private void ContextControl_CloseRequested(object? sender, EventArgs e) => CloseContextMenu();

    private void ContextControl_FocusSearchRequested(object? sender, EventArgs e) =>
        FocusSearchRequested?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        CloseContextMenu();
        ContextControl.CloseRequested -= ContextControl_CloseRequested;
        ContextControl.FocusSearchRequested -= ContextControl_FocusSearchRequested;
        SetCommandContext(null);
        GC.SuppressFinalize(this);
    }
}
