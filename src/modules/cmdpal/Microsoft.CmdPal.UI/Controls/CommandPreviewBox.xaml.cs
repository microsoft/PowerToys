// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class CommandPreviewBox : UserControl
{
    private string _title = string.Empty;
    private string _subtitle = string.Empty;

    public CommandPreviewBox()
    {
        InitializeComponent();
    }

    public void Configure(string title, string subtitle, IconInfoViewModel? icon)
    {
        _title = title ?? string.Empty;
        _subtitle = subtitle ?? string.Empty;

        var hasIcon = icon?.IsSet == true;
        PreviewIcon.SourceKey = icon;
        PreviewIcon.Visibility = hasIcon ? Visibility.Visible : Visibility.Collapsed;
        PreviewTextPanel.Margin = hasIcon ? new Thickness(8, 0, 0, 0) : default;

        SetTextVisibility(showTitle: true, showSubtitle: true);
    }

    public void SetTextVisibility(bool showTitle, bool showSubtitle)
    {
        showTitle &= !string.IsNullOrEmpty(_title);
        showSubtitle &= !string.IsNullOrEmpty(_subtitle);

        PreviewTitleText.Text = showTitle ? _title : string.Empty;
        PreviewTitleText.Visibility = showTitle ? Visibility.Visible : Visibility.Collapsed;

        PreviewSubtitleText.Text = showSubtitle ? _subtitle : string.Empty;
        PreviewSubtitleText.Visibility = showSubtitle ? Visibility.Visible : Visibility.Collapsed;

        PreviewTextPanel.Visibility = showTitle || showSubtitle ? Visibility.Visible : Visibility.Collapsed;
    }
}
