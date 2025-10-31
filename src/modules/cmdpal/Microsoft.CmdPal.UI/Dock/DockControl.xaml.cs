// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Microsoft.CmdPal.UI.Dock;

public sealed partial class DockControl : UserControl, INotifyPropertyChanged, IRecipient<CloseContextMenuMessage>
{
    private DockViewModel _viewModel;

    internal DockViewModel ViewModel => _viewModel;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Orientation ItemsOrientation
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(ItemsOrientation)));
            }
        }
    }

    public double IconSize
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(IconSize)));
                PropertyChanged?.Invoke(this, new(nameof(IconMinWidth)));
            }
        }
    }

= 16.0;

    public double IconMinWidth => IconSize / 2;

    public double TitleTextMaxWidth
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(TitleTextMaxWidth)));
            }
        }
    }

= 100;

    public double TitleTextFontSize
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(TitleTextFontSize)));
            }
        }
    }

= 12;

    // TODO! Remove me
    public bool ShowSearchButton
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(ShowSearchButton)));
            }
        }
    }

    internal DockControl(DockViewModel viewModel)
    {
        // MainViewModel mainModel = (MainViewModel)DataContext;
        _viewModel = viewModel;
        InitializeComponent();
        WeakReferenceMessenger.Default.Register<CloseContextMenuMessage>(this);
    }

    internal void UpdateSettings(DockSettings settings)
    {
        var isHorizontal = settings.Side == DockSide.Top || settings.Side == DockSide.Bottom;

        // _viewModel.UpdateSettings(); // TODO!
        ItemsOrientation = isHorizontal ? Orientation.Horizontal : Orientation.Vertical;

        // ShowSearchButton = settings.ShowSearchButton;
        SearchColumn.Width = ShowSearchButton
            ? new Microsoft.UI.Xaml.GridLength(1, Microsoft.UI.Xaml.GridUnitType.Star)
            : new Microsoft.UI.Xaml.GridLength(0, Microsoft.UI.Xaml.GridUnitType.Star);

        EndColumn.Width = ShowSearchButton
            ? new Microsoft.UI.Xaml.GridLength(2, Microsoft.UI.Xaml.GridUnitType.Star)
            : new Microsoft.UI.Xaml.GridLength(1, Microsoft.UI.Xaml.GridUnitType.Auto);

        IconSize = DockSettingsToViews.IconSizeForSize(settings.DockIconsSize);
        TitleTextFontSize = DockSettingsToViews.TitleTextFontSizeForSize(settings.DockSize);
        TitleTextMaxWidth = DockSettingsToViews.TitleTextMaxWidthForSize(settings.DockSize);
    }

    [RelayCommand]
    private void SearchOpenCmdPal(object? whatever)
    {
        // URI invoke "x-cmdpal://"
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "x-cmdpal://",
            UseShellExecute = true,
        });
    }

    private void BandItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        var pos = e.GetPosition(null);
        var button = sender as Button;
        var item = button?.DataContext as DockItemViewModel;

        if (item is not null)
        {
            InvokeItem(item, pos);
        }
    }

    private void BandItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        var pos = e.GetPosition(null);
        var button = sender as Button;
        var item = button?.DataContext as DockItemViewModel;
        if (item is not null)
        {
            if (item.HasMoreCommands)
            {
                ContextControl.ViewModel.SelectedItem = item;
                ContextMenuFlyout.ShowAt(
                button,
                new FlyoutShowOptions()
                {
                    ShowMode = FlyoutShowMode.Standard,
                    Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                });
                e.Handled = true;
            }
        }
    }

    private void InvokeItem(DockItemViewModel item, global::Windows.Foundation.Point pos)
    {
        var command = item.Command;
        try
        {
            var isPage = command.Model.Unsafe is not IInvokableCommand invokable;
            if (isPage)
            {
                // TODO! stick the logic here to find the right place to open
                // the window
                WeakReferenceMessenger.Default.Send<ShowWindowMessage>(new(0));
            }

            PerformCommandMessage m = new(command.Model);
            WeakReferenceMessenger.Default.Send(m);
        }
        catch (COMException e)
        {
            Logger.LogError("Error invoking dock command", e);
        }
    }

    private void ContextMenuFlyout_Opened(object sender, object e)
    {
        // We need to wait until our flyout is opened to try and toss focus
        // at its search box. The control isn't in the UI tree before that
        ContextControl.FocusSearchBox();
    }

    public void Receive(CloseContextMenuMessage message)
    {
        if (ContextMenuFlyout.IsOpen)
        {
            ContextMenuFlyout.Hide();
        }
    }
}
