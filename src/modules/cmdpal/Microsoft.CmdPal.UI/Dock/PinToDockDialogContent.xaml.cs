// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Dock;

public sealed partial class PinToDockDialogContent : UserControl
{
    private string _title = string.Empty;
    private string _subtitle = string.Empty;

    public DockPinSide SelectedSide => SectionSegmented.SelectedIndex switch
    {
        0 => DockPinSide.Start,
        1 => DockPinSide.Center,
        2 => DockPinSide.End,
        _ => DockPinSide.Start,
    };

    public bool? ShowTitles => ShowTitleCheckBox.IsChecked;

    public bool? ShowSubtitles => ShowSubtitleCheckBox.IsChecked;

    public PinToDockDialogContent()
    {
        InitializeComponent();
    }

    public void Configure(string title, string subtitle, IconInfoViewModel? icon, DockSide dockSide)
    {
        _title = title;
        _subtitle = subtitle;

        var hasTitle = !string.IsNullOrEmpty(title);
        var hasSubtitle = !string.IsNullOrEmpty(subtitle);

        PreviewTitleText.Text = title;
        PreviewTitleText.Visibility = hasTitle ? Visibility.Visible : Visibility.Collapsed;

        PreviewSubtitleText.Text = subtitle;
        PreviewSubtitleText.Visibility = hasSubtitle ? Visibility.Visible : Visibility.Collapsed;

        PreviewTextPanel.Visibility = (hasTitle || hasSubtitle) ? Visibility.Visible : Visibility.Collapsed;

        ShowTitleCheckBox.Visibility = hasTitle ? Visibility.Visible : Visibility.Collapsed;
        ShowTitleCheckBox.IsChecked = hasTitle;

        ShowSubtitleCheckBox.Visibility = hasSubtitle ? Visibility.Visible : Visibility.Collapsed;
        ShowSubtitleCheckBox.IsChecked = hasSubtitle;

        if (icon is not null)
        {
            PreviewIcon.SourceKey = icon;
        }

        ApplyDockOrientation(dockSide);
    }

    public static async System.Threading.Tasks.Task<(ContentDialogResult Result, PinToDockDialogContent Content)> ShowAsync(
        XamlRoot xamlRoot,
        string title,
        string subtitle,
        IconInfoViewModel? icon,
        DockSide dockSide)
    {
        var content = new PinToDockDialogContent();
        content.Configure(title, subtitle, icon, dockSide);

        var dialog = new ContentDialog
        {
            Title = RS_.GetString("PinToDock_DialogTitle"),
            Content = content,
            PrimaryButtonText = RS_.GetString("PinToDock_PinButton"),
            CloseButtonText = RS_.GetString("PinToDock_CancelButton"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };

        // Inner controls (Segmented, CheckBox) may consume the Enter key event,
        // preventing DefaultButton from activating. Handle it explicitly.
        var enterPressed = false;
        dialog.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler((s, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    enterPressed = true;
                    dialog.Hide();
                }
            }),
            true);

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.None && enterPressed)
        {
            result = ContentDialogResult.Primary;
        }

        return (result, content);
    }

    private void ApplyDockOrientation(DockSide dockSide)
    {
        var isVertical = dockSide is DockSide.Left or DockSide.Right;

        if (isVertical)
        {
            StartSegmentedItem.Content = RS_.GetString("PinToDock_Top");
            CenterSegmentedItem.Content = RS_.GetString("PinToDock_CenterLabel");
            EndSegmentedItem.Content = RS_.GetString("PinToDock_Bottom");
        }
        else
        {
            StartSegmentedItem.Content = RS_.GetString("PinToDock_Left");
            CenterSegmentedItem.Content = RS_.GetString("PinToDock_CenterLabel");
            EndSegmentedItem.Content = RS_.GetString("PinToDock_RightLabel");
        }

        // Pick the 3 icon path strings based on dock orientation
        var (startKey, centerKey, endKey) = dockSide switch
        {
            DockSide.Top => ("TopStartPath", "TopCenterPath", "TopEndPath"),
            DockSide.Bottom => ("BottomStartPath", "BottomCenterPath", "BottomEndPath"),
            DockSide.Left => ("LeftStartPath", "LeftCenterPath", "LeftEndPath"),
            DockSide.Right => ("RightStartPath", "RightCenterPath", "RightEndPath"),
            _ => ("TopStartPath", "TopCenterPath", "TopEndPath"),
        };

        StartSegmentedItem.Icon = CreatePathIcon((string)Resources[startKey]);
        CenterSegmentedItem.Icon = CreatePathIcon((string)Resources[centerKey]);
        EndSegmentedItem.Icon = CreatePathIcon((string)Resources[endKey]);
    }

    private static PathIcon CreatePathIcon(string pathData)
    {
        var geometry = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
            typeof(Microsoft.UI.Xaml.Media.Geometry), pathData);
        return new PathIcon { Data = geometry };
    }

    private void OnLabelOptionChanged(object sender, RoutedEventArgs e)
    {
        if (PreviewTitleText is null || PreviewSubtitleText is null ||
            PreviewTextPanel is null || ShowTitleCheckBox is null || ShowSubtitleCheckBox is null)
        {
            return;
        }

        var showTitle = ShowTitleCheckBox.IsChecked == true;
        var showSubtitle = ShowSubtitleCheckBox.IsChecked == true;

        PreviewTitleText.Text = showTitle ? _title : string.Empty;
        PreviewTitleText.Visibility = showTitle ? Visibility.Visible : Visibility.Collapsed;

        PreviewSubtitleText.Text = showSubtitle ? _subtitle : string.Empty;
        PreviewSubtitleText.Visibility = showSubtitle ? Visibility.Visible : Visibility.Collapsed;

        PreviewTextPanel.Visibility = (showTitle || showSubtitle) ? Visibility.Visible : Visibility.Collapsed;
    }
}
