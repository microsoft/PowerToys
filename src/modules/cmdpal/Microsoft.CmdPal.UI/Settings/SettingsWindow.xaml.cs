// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIEx;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;
using TitleBar = Microsoft.UI.Xaml.Controls.TitleBar;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class SettingsWindow : WindowEx,
    IRecipient<NavigateToExtensionSettingsMessage>,
    IRecipient<QuitMessage>
{
    public ObservableCollection<Crumb> BreadCrumbs { get; } = [];

    // Gets or sets optional action invoked after NavigationView is loaded.
    public Action? NavigationViewLoaded { get; set; }

    internal string? OpenToPage { get; set; }

    public SettingsWindow()
    {
        this.InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.SetIcon();
        var title = RS_.GetString("SettingsWindowTitle");
        this.AppWindow.Title = title;
        this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        this.AppTitleBar.Title = title;
        PositionCentered();

        WeakReferenceMessenger.Default.Register<NavigateToExtensionSettingsMessage>(this);
        WeakReferenceMessenger.Default.Register<QuitMessage>(this);
    }

    // Handles NavigationView loaded event.
    // Sets up initial navigation and accessibility notifications.
    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // Delay necessary to ensure NavigationView visual state can match navigation
        Task.Delay(500).ContinueWith(_ => this.NavigationViewLoaded?.Invoke(), TaskScheduler.FromCurrentSynchronizationContext());

        NavView.SelectedItem = NavView.MenuItems[0];

        Navigate(OpenToPage);
        OpenToPage = null;

        if (sender is NavigationView navigationView)
        {
            // Register for pane open/close changes to announce to screen readers
            navigationView.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, AnnounceNavigationPaneStateChanged);
        }
    }

    // Announces navigation pane open/close state to screen readers for accessibility.
    private void AnnounceNavigationPaneStateChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (sender is NavigationView navigationView)
        {
            UIHelper.AnnounceActionForAccessibility(
            ue: (UIElement)sender,
            (sender as NavigationView)?.IsPaneOpen == true ? RS_.GetString("NavigationPaneOpened") : RS_.GetString("NavigationPaneClosed"),
            "NavigationViewPaneIsOpenChangeNotificationId");
        }
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var selectedItem = args.InvokedItemContainer;
        Navigate((selectedItem.Tag as string)!);
    }

    internal void Navigate(string? page)
    {
        var pageType = page switch
        {
            null => typeof(GeneralPage),
            "General" => typeof(GeneralPage),
            "Extensions" => typeof(ExtensionsPage),
            "Dock" => typeof(DockSettingsPage),
            _ => null,
        };
        var actualPage = page ?? "General";
        if (pageType is not null)
        {
            BreadCrumbs.Clear();
            BreadCrumbs.Add(new(actualPage, actualPage));
            NavFrame.Navigate(pageType);

            // Now, make sure to actually select the correct menu item too
            foreach (var obj in NavView.MenuItems)
            {
                if (obj is NavigationViewItem item)
                {
                    if (item.Tag is string s && s == page)
                    {
                        NavView.SelectedItem = item;
                    }
                }
            }
        }
    }

    private void Navigate(ProviderSettingsViewModel extension)
    {
        NavFrame.Navigate(typeof(ExtensionPage), extension);
        BreadCrumbs.Add(new(extension.DisplayName, string.Empty));
    }

    private void PositionCentered()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        if (displayArea is not null)
        {
            var centeredPosition = AppWindow.Position;
            centeredPosition.X = (displayArea.WorkArea.Width - AppWindow.Size.Width) / 2;
            centeredPosition.Y = (displayArea.WorkArea.Height - AppWindow.Size.Height) / 2;
            AppWindow.Move(centeredPosition);
        }
    }

    public void Receive(NavigateToExtensionSettingsMessage message) => Navigate(message.ProviderSettingsVM);

    private void NavigationBreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Item is Crumb crumb)
        {
            if (crumb.Data is string data)
            {
                if (!string.IsNullOrEmpty(data))
                {
                    Navigate(data);
                }
            }
        }
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        WeakReferenceMessenger.Default.Send<WindowActivatedEventArgs>(args);
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        WeakReferenceMessenger.Default.Send<SettingsWindowClosedMessage>();

        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private void NavView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        if (args.DisplayMode == NavigationViewDisplayMode.Compact || args.DisplayMode == NavigationViewDisplayMode.Minimal)
        {
            AppTitleBar.IsPaneToggleButtonVisible = true;
            WorkAroundIcon.Margin = new Thickness(8, 0, 16, 0); // Required for workaround, see XAML comment
        }
        else
        {
            AppTitleBar.IsPaneToggleButtonVisible = false;
            WorkAroundIcon.Margin = new Thickness(16, 0, 0, 0); // Required for workaround, see XAML comment
        }
    }

    public void Receive(QuitMessage message)
    {
        // This might come in on a background thread
        DispatcherQueue.TryEnqueue(() => Close());
    }

    private void AppTitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }
}

public readonly struct Crumb
{
    public Crumb(string label, object data)
    {
        Label = label;
        Data = data;
    }

    public string Label { get; }

    public object Data { get; }

    public override string ToString() => Label;
}
