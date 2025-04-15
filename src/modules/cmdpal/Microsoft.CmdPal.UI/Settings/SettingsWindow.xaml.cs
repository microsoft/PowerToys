// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class SettingsWindow : Window,
    IRecipient<NavigateToExtensionSettingsMessage>,
    IRecipient<QuitMessage>
{
    public ObservableCollection<Crumb> BreadCrumbs { get; } = [];

    public SettingsWindow()
    {
        this.InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.SetIcon();
        this.AppWindow.Title = RS_.GetString("SettingsWindowTitle");
        this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        PositionCentered();

        WeakReferenceMessenger.Default.Register<NavigateToExtensionSettingsMessage>(this);
        WeakReferenceMessenger.Default.Register<QuitMessage>(this);
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.SelectedItem = NavView.MenuItems[0];
        Navigate("General");
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var selectedItem = args.InvokedItemContainer;
        Navigate((selectedItem.Tag as string)!);
    }

    private void Navigate(string page)
    {
        var pageType = page switch
        {
            "General" => typeof(GeneralPage),
            "Extensions" => typeof(ExtensionsPage),
            _ => null,
        };
        if (pageType is not null)
        {
            BreadCrumbs.Clear();
            BreadCrumbs.Add(new(page, page));
            NavFrame.Navigate(pageType);
        }
    }

    private void Navigate(ProviderSettingsViewModel extension)
    {
        NavFrame.Navigate(typeof(ExtensionPage), extension);
        BreadCrumbs.Add(new(extension.DisplayName, string.Empty));
    }

    private void PositionCentered()
    {
        AppWindow.Resize(new SizeInt32 { Width = 1280, Height = 720 });
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
    }

    private void PaneToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void NavView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        if (args.DisplayMode == NavigationViewDisplayMode.Compact || args.DisplayMode == NavigationViewDisplayMode.Minimal)
        {
            PaneToggleBtn.Visibility = Visibility.Visible;
            NavView.IsPaneToggleButtonVisible = false;
            AppTitleBar.Margin = new Thickness(48, 0, 0, 0);
        }
        else
        {
            PaneToggleBtn.Visibility = Visibility.Collapsed;
            NavView.IsPaneToggleButtonVisible = true;
            AppTitleBar.Margin = new Thickness(16, 0, 0, 0);
        }
    }

    public void Receive(QuitMessage message)
    {
        // This might come in on a background thread
        DispatcherQueue.TryEnqueue(() => Close());
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
