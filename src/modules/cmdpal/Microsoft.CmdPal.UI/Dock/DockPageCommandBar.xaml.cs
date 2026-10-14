// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Point = Windows.Foundation.Point;
using VirtualKey = Windows.System.VirtualKey;

namespace Microsoft.CmdPal.UI.Dock;

public sealed partial class DockPageCommandBar : UserControl, ICommandBarInteractionTarget, IDisposable
{
    private readonly ContextMenuHost _menuHost;
    private long _commandContextVersion;
    private ICommandBarContext? _commandContext;

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
        _menuHost = new ContextMenuHost(
            () => new(MoreCommandsButton, null, FlyoutPlacementMode.TopEdgeAlignedRight, ContextMenuFilterLocation.Bottom),
            ContextMenuFlyout);
        ContextMenuFlyout.BackRequested += ContextMenuFlyout_BackRequested;
        Unloaded += DockPageCommandBar_Unloaded;
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

        _commandContext = context;
        ViewModel.SetContext(context);
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
        _menuHost.Show(new ContextMenuRequest(context)
        {
            Anchor = new(target, position, placement, filterLocation),
        });
    }

    public bool TryCommandKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        var context = _commandContext;
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
            _menuHost.ShowAfterKeyEvent(() =>
                ReferenceEquals(context, _commandContext) && context.AllCommands.Contains(command)
                    ? new ContextMenuRequest(context) { InitialSubmenu = command }
                    : null);
        }
        else
        {
            ViewModel.InvokeContextCommand(command);
        }

        return true;
    }

    public void OpenContextMenu() => OpenSelectedItemContextMenu();

    internal void OpenSelectedItemContextMenu()
    {
        if (_commandContext is ICommandBarContext context)
        {
            ShowContextMenu(
                context,
                MoreCommandsButton,
                FlyoutPlacementMode.TopEdgeAlignedRight,
                null,
                ContextMenuFilterLocation.Bottom);
        }
    }

    public void CloseContextMenu() => _menuHost.Close();

    private void PrimaryButton_Click(object sender, RoutedEventArgs e) => ViewModel.InvokePrimaryCommand();

    private void SecondaryButton_Click(object sender, RoutedEventArgs e) => ViewModel.InvokeSecondaryCommand();

    private void MoreCommandsButton_Click(object sender, RoutedEventArgs e) => OpenSelectedItemContextMenu();

    private void ContextMenuFlyout_BackRequested(object? sender, EventArgs e) =>
        FocusSearchRequested?.Invoke(this, EventArgs.Empty);

    private void DockPageCommandBar_Unloaded(object sender, RoutedEventArgs e)
    {
        CloseContextMenu();
        ViewModel.ClearContext();
    }

    public void Dispose()
    {
        CloseContextMenu();
        ContextMenuFlyout.BackRequested -= ContextMenuFlyout_BackRequested;
        Unloaded -= DockPageCommandBar_Unloaded;
        SetCommandContext(null);
        ViewModel.ClearContext();
        GC.SuppressFinalize(this);
    }
}
