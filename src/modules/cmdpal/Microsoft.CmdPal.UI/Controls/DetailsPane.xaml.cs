// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// Shows the details (hero image, title, body, content, and metadata) of a list item or page.
/// </summary>
public sealed partial class DetailsPane : UserControl
{
    public static readonly DependencyProperty DetailsProperty =
        DependencyProperty.Register(nameof(Details), typeof(DetailsViewModel), typeof(DetailsPane), new PropertyMetadata(null, OnDetailsChanged));

    public DetailsPane()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) => UpdateHeroImageVisibility();
    }

    public DetailsViewModel? Details
    {
        get => (DetailsViewModel?)GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    /// <summary>
    /// Gets or sets the page that owns <see cref="Details"/>. Commands in the details are
    /// sent with this page as their source, so a dock flyout can route them back to itself.
    /// </summary>
    public PageViewModel? SourcePage { get; set; }

    private static void OnDetailsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((DetailsPane)d).UpdateHeroImageVisibility();

    // The view model doesn't know the current theme, so the view decides if the
    // hero image has an icon for it.
    private void UpdateHeroImageVisibility()
    {
        var hasHeroImage = Details?.HeroImage?.HasIcon(ActualTheme == ElementTheme.Light) ?? false;
        HeroImageBorder.Visibility = hasHeroImage ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Command_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is CommandViewModel commandViewModel)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(commandViewModel.Model, SourcePage));
        }
    }
}
