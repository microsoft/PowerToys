// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class SettingsWindowViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly GeneralPage _generalPage;
    private readonly AppearancePage _appearancePage;
    private readonly ExtensionsPage _extensionsPage;
    private readonly ExtensionPage _extensionPage;

    private readonly Stack<NavItem> _navigationStack = new();

    public bool CanGoBack => _navigationStack.Count > 1;

    [ObservableProperty]
    public partial Page CurrentPage { get; private set; }

    public ObservableCollection<Crumb> BreadCrumbs { get; } = [];

    public SettingsWindowViewModel(
        GeneralPage generalPage,
        AppearancePage appearancePage,
        ExtensionsPage extensionsPage,
        ExtensionPage extensionPage,
        ILogger logger)
    {
        _generalPage = generalPage;
        _appearancePage = appearancePage;
        _extensionsPage = extensionsPage;
        _extensionPage = extensionPage;
        _logger = logger;

        _navigationStack.Push(new("General", null));
        CurrentPage = _generalPage;
    }

    public void Navigate(string page)
    {
        string? pageType = null;
        BreadCrumbs.Clear();

        switch (page)
        {
            case "General":
                _navigationStack.Push(new("General", null));
                CurrentPage = _generalPage;
                pageType = RS_.GetString("Settings_PageTitles_GeneralPage");
                BreadCrumbs.Add(new(pageType, pageType));
                break;
            case "Appearance":
                _navigationStack.Push(new("Appearance", null));
                CurrentPage = _appearancePage;
                pageType = RS_.GetString("Settings_PageTitles_AppearancePage");
                BreadCrumbs.Add(new(pageType, pageType));
                break;
            case "Extensions":
                _navigationStack.Push(new("Extensions", null));
                CurrentPage = _extensionsPage;
                pageType = RS_.GetString("Settings_PageTitles_ExtensionsPage");
                BreadCrumbs.Add(new(pageType, pageType));
                break;
            default:
                BreadCrumbs.Add(new($"[{CurrentPage.GetType()?.Name}]", string.Empty));
                Log_UnknownBreadcrumbForPageType(CurrentPage.GetType());
                break;
        }

        OnPropertyChanged(nameof(CanGoBack));
    }

    public void Navigate(ProviderSettingsViewModel extension)
    {
        BreadCrumbs.Clear();

        _navigationStack.Push(new("Extension", extension));
        _extensionPage.OnNavigatedTo(extension);
        CurrentPage = _extensionPage;

        var extensionsPageType = RS_.GetString("Settings_PageTitles_ExtensionsPage");
        BreadCrumbs.Add(new(extensionsPageType, extensionsPageType));
        BreadCrumbs.Add(new(extension.DisplayName, extension));

        OnPropertyChanged(nameof(CanGoBack));
    }

    public void TryGoBack()
    {
        if (_navigationStack.Count > 1)
        {
            _navigationStack.Pop();
            if (_navigationStack.Count > 0)
            {
                var navItem = _navigationStack.Pop();
                if (navItem.Page.Equals("Extension", StringComparison.Ordinal))
                {
                    Navigate(navItem.ProviderSettingsViewModel!);
                }
                else
                {
                    Navigate(navItem.Page);
                }
            }
            else
            {
                Navigate("General");
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Unknown breadcrumb for page type '{sourcePageType}'")]
    partial void Log_UnknownBreadcrumbForPageType(Type? sourcePageType);
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

#pragma warning disable SA1402 // File may only contain a single type
internal sealed record NavItem(string Page, ProviderSettingsViewModel? ProviderSettingsViewModel);
#pragma warning restore SA1402 // File may only contain a single type
