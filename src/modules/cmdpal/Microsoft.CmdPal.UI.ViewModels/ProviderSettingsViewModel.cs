// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Properties;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ProviderSettingsViewModel(
    CommandProviderWrapper _provider,
    ProviderSettings _providerSettings,
    IServiceProvider _serviceProvider) : ObservableObject
{
    private readonly SettingsModel _settings = _serviceProvider.GetService<SettingsModel>()!;

    public string DisplayName => _provider.DisplayName;

    public string ExtensionName => _provider.Extension?.ExtensionDisplayName ?? "Built-in";

    public string ExtensionSubtext => IsEnabled ? $"{ExtensionName}, {TopLevelCommands.Count} commands" : Resources.builtin_disabled_extension;

    [MemberNotNullWhen(true, nameof(Extension))]
    public bool IsFromExtension => _provider.Extension != null;

    public IExtensionWrapper? Extension => _provider.Extension;

    public string ExtensionVersion => IsFromExtension ? $"{Extension.Version.Major}.{Extension.Version.Minor}.{Extension.Version.Build}.{Extension.Version.Revision}" : string.Empty;

    public IconInfoViewModel Icon => _provider.Icon;

    public bool IsEnabled
    {
        get => _providerSettings.IsEnabled;
        set
        {
            if (value != _providerSettings.IsEnabled)
            {
                _providerSettings.IsEnabled = value;
                Save();
                WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(ExtensionSubtext));
            }

            if (value == true)
            {
                _provider.CommandsChanged -= Provider_CommandsChanged;
                _provider.CommandsChanged += Provider_CommandsChanged;
            }
        }
    }

    private void Provider_CommandsChanged(CommandProviderWrapper sender, CommandPalette.Extensions.IItemsChangedEventArgs args)
    {
        OnPropertyChanged(nameof(ExtensionSubtext));
        OnPropertyChanged(nameof(TopLevelCommands));
    }

    public bool HasSettings => _provider.Settings != null && _provider.Settings.SettingsPage != null;

    public ContentPageViewModel? SettingsPage => HasSettings ? _provider?.Settings?.SettingsPage : null;

    [field: AllowNull]
    public List<TopLevelViewModel> TopLevelCommands
    {
        get
        {
            if (field == null)
            {
                field = BuildTopLevelViewModels();
            }

            return field;
        }
    }

    private List<TopLevelViewModel> BuildTopLevelViewModels()
    {
        var thisProvider = _provider;
        var providersCommands = thisProvider.TopLevelItems;

        // Remember! This comes in on the UI thread!
        return [.. providersCommands];
    }

    private void Save() => SettingsModel.SaveSettings(_settings);
}
